using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Models;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.PrBot.Models;
using Aiursoft.NugetNinja.PrBot.Services.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.PrBot;

public class Entry(
    IOptions<List<Server>> servers,
    IEnumerable<IVersionControlService> versionControls,
    RunAllOfficialPluginsService runAllOfficialPluginsService,
    WorkspaceManager workspaceManager,
    ILogger<Entry> logger)
{
    private readonly List<Server> _servers = servers.Value;

    private readonly string _workspaceFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NugetNinjaWorkspace");

    public async Task RunAsync()
    {
        logger.LogInformation("Starting Nuget Ninja PR bot...");

        foreach (var server in _servers)
        {
            logger.LogInformation("Processing server: {ServerProvider}...", server.Provider);
            var serviceProvider = versionControls.First(v => v.GetName() == server.Provider);
            await RunServerAsync(server, serviceProvider);
        }
    }

    private async Task RunServerAsync(Server server, IVersionControlService versionControl)
    {
        var myStars = await versionControl
            .GetMyStars(server.EndPoint, server.UserName, server.Token)
            .Where(r => r.Archived == false)
            .Where(r => r.Owner?.Login != server.UserName)
            .ToListAsync();

        logger.LogInformation("Got {MyStarsCount} stared repositories as registered to create pull requests automatically", myStars.Count);
        logger.LogInformation("\r\n\r\n");
        logger.LogInformation("================================================================");
        logger.LogInformation("\r\n\r\n");
        foreach (var repo in myStars)
            try
            {
                logger.LogInformation("Processing repository {Repo}...", repo);
                await ProcessRepository(repo, server, versionControl);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Crashed when processing repo: {Repo}!", repo);
            }
            finally
            {
                logger.LogInformation("\r\n\r\n");
                logger.LogInformation("================================================================");
                logger.LogInformation("\r\n\r\n");
            }
    }

    private async Task ProcessRepository(Repository repo, Server connectionConfiguration,
        IVersionControlService versionControl)
    {
        if (string.IsNullOrWhiteSpace(repo.Owner?.Login) || string.IsNullOrWhiteSpace(repo.Name))
            throw new InvalidDataException($"The repo with path: {repo} is having invalid data!");

        // Clone locally.
        var workPath = Path.Combine(_workspaceFolder, $"{repo.Id}-{repo.Name}");
        logger.LogInformation("Cloning repository: {RepoName} to {WorkPath}...", repo.Name, workPath);
        await workspaceManager.ResetRepo(
            workPath,
            repo.DefaultBranch ?? throw new NullReferenceException($"The default branch of {repo} is null!"),
            repo.CloneUrl ?? throw new NullReferenceException($"The clone endpoint branch of {repo} is null!"),
            CloneMode.Full,
            $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}");

        // Run all plugins.
        await runAllOfficialPluginsService.RunAllPlugins(workPath, true, onlyRunUpdatePlugin: connectionConfiguration.OnlyUpdate);

        // Consider changes...
        if (!await workspaceManager.PendingCommit(workPath))
        {
            logger.LogInformation("{Repo} has no suggestion that we can make. Ignore", repo);
            return;
        }

        logger.LogInformation("{Repo} is pending some fix. We will try to create\\\\update related pull request", repo);
        await workspaceManager.SetUserConfig(workPath, connectionConfiguration.DisplayName,
            connectionConfiguration.UserEmail);
        var saved = await workspaceManager.CommitToBranch(workPath, "Auto csproj fix and update by bot.",
            connectionConfiguration.ContributionBranch);
        if (!saved)
        {
            logger.LogInformation("{Repo} has no suggestion that we can make. Ignore", repo);
            return;
        }

        // Fork repo.
        if (!await versionControl.RepoExists(
                connectionConfiguration.EndPoint,
                connectionConfiguration.UserName,
                repo.Name,
                connectionConfiguration.Token))
        {
            await versionControl.ForkRepo(
                connectionConfiguration.EndPoint,
                repo.Owner.Login,
                repo.Name,
                connectionConfiguration.Token);
            await Task.Delay(5000);
            while (!await versionControl.RepoExists(
                       connectionConfiguration.EndPoint,
                       connectionConfiguration.UserName,
                       repo.Name,
                       connectionConfiguration.Token))
                // Wait a while. GitHub may need some time to fork the repo.
                await Task.Delay(5000);
        }

        // Push to forked repo.
        var pushPath = versionControl.GetPushPath(connectionConfiguration, repo);

        await workspaceManager.Push(
            workPath,
            connectionConfiguration.ContributionBranch,
            pushPath,
            true);

        var existingPullRequestsByBot = (await versionControl.GetPullRequests(
                connectionConfiguration.EndPoint,
                repo.Owner.Login,
                repo.Name,
                $"{connectionConfiguration.UserName}:{connectionConfiguration.ContributionBranch}",
                connectionConfiguration.Token))
            .Where(p => string.Equals(p.User?.Login, connectionConfiguration.UserName,
                StringComparison.OrdinalIgnoreCase));

        if (existingPullRequestsByBot.All(p => p.State != "open"))
            // Create a new pull request.
            await versionControl.CreatePullRequest(
                connectionConfiguration.EndPoint,
                repo.Owner.Login,
                repo.Name,
                $"{connectionConfiguration.UserName}:{connectionConfiguration.ContributionBranch}",
                repo.DefaultBranch,
                "Auto dependencies upgrade by bot.",
                @"
Auto dependencies upgrade by bot. This is automatically generated by bot.

The bot tries to fetch all possible updates and modify the project files automatically.

This pull request may break or change the behavior of this application. Review with cautious!",
                connectionConfiguration.Token);
        else
            logger.LogInformation("Skipped creating new pull request for {Repo} because there already exists", repo);
    }
}
