using Aiursoft.NugetNinja.AllOfficialsPlugin;
using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.PrBot;

public class Entry
{
    private readonly ILogger<Entry> _logger;
    private readonly RunAllOfficialPluginsService _runAllOfficialPluginsService;
    private readonly List<Server> _servers;
    private readonly IEnumerable<IVersionControlService> _versionControls;

    private readonly string _workspaceFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NugetNinjaWorkspace");

    private readonly WorkspaceManager _workspaceManager;

    public Entry(
        IOptions<List<Server>> servers,
        IEnumerable<IVersionControlService> versionControls,
        RunAllOfficialPluginsService runAllOfficialPluginsService,
        WorkspaceManager workspaceManager,
        ILogger<Entry> logger)
    {
        _servers = servers.Value;
        _versionControls = versionControls;
        _runAllOfficialPluginsService = runAllOfficialPluginsService;
        _workspaceManager = workspaceManager;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Nuget Ninja PR bot...");

        foreach (var server in _servers)
        {
            _logger.LogInformation("Processing server: {ServerProvider}...", server.Provider);
            var serviceProvider = _versionControls.First(v => v.GetName() == server.Provider);
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

        _logger.LogInformation("Got {MyStarsCount} stared repositories as registered to create pull requests automatically", myStars.Count);
        _logger.LogInformation("\r\n\r\n");
        _logger.LogInformation("================================================================");
        _logger.LogInformation("\r\n\r\n");
        foreach (var repo in myStars)
            try
            {
                _logger.LogInformation("Processing repository {Repo}...", repo);
                await ProcessRepository(repo, server, versionControl);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Crashed when processing repo: {Repo}!", repo);
            }
            finally
            {
                _logger.LogInformation("\r\n\r\n");
                _logger.LogInformation("================================================================");
                _logger.LogInformation("\r\n\r\n");
            }
    }

    private async Task ProcessRepository(Repository repo, Server connectionConfiguration,
        IVersionControlService versionControl)
    {
        if (string.IsNullOrWhiteSpace(repo.Owner?.Login) || string.IsNullOrWhiteSpace(repo.Name))
            throw new InvalidDataException($"The repo with path: {repo} is having invalid data!");

        // Clone locally.
        var workPath = Path.Combine(_workspaceFolder, $"{repo.Id}-{repo.Name}");
        _logger.LogInformation("Cloning repository: {RepoName} to {WorkPath}...", repo.Name, workPath);
        await _workspaceManager.ResetRepo(
            workPath,
            repo.DefaultBranch ?? throw new NullReferenceException($"The default branch of {repo} is null!"),
            repo.CloneUrl ?? throw new NullReferenceException($"The clone endpoint branch of {repo} is null!"));

        // Run all plugins.
        await _runAllOfficialPluginsService.OnServiceStartedAsync(workPath, true, onlyUpdate: connectionConfiguration.OnlyUpdate);

        // Consider changes...
        if (!await _workspaceManager.PendingCommit(workPath))
        {
            _logger.LogInformation("{Repo} has no suggestion that we can make. Ignore", repo);
            return;
        }

        _logger.LogInformation("{Repo} is pending some fix. We will try to create\\\\update related pull request", repo);
        await _workspaceManager.SetUserConfig(workPath, connectionConfiguration.DisplayName,
            connectionConfiguration.UserEmail);
        var saved = await _workspaceManager.CommitToBranch(workPath, "Auto csproj fix and update by bot.",
            connectionConfiguration.ContributionBranch);
        if (!saved)
        {
            _logger.LogInformation("{Repo} has no suggestion that we can make. Ignore", repo);
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

        await _workspaceManager.Push(
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
                connectionConfiguration.Token);
        else
            _logger.LogInformation("Skipped creating new pull request for {Repo} because there already exists", repo);
    }
}