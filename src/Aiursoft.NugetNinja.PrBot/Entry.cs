using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Models;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Services; // New using statement
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;
using Aiursoft.NugetNinja.PrBot.Configuration; // New using statement
using Aiursoft.NugetNinja.PrBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.PrBot;

public class Entry(
    IOptions<List<Server>> servers,
    IEnumerable<IVersionControlService> versionControls,
    RunAllOfficialPluginsService runAllOfficialPluginsService,
    WorkspaceManager workspaceManager,
    LocalizationService localizationService,
    TokenManagementService tokenManagementService, // Changed
    IOptions<PrBotOptions> prBotOptions,
    ILogger<Entry> logger)
{
    private readonly List<Server> _servers = servers.Value;
    private readonly TokenManagementService _tokenManagementService = tokenManagementService;
    private readonly string _workspaceFolder = prBotOptions.Value.WorkspacePath;
    private readonly ILogger<Entry> _logger = logger;
    private readonly IEnumerable<IVersionControlService> _versionControls = versionControls;
    private readonly RunAllOfficialPluginsService _runAllOfficialPluginsService = runAllOfficialPluginsService;
    private readonly WorkspaceManager _workspaceManager = workspaceManager;
    private readonly LocalizationService _localizationService = localizationService;

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Nuget Ninja PR bot...");

        foreach (var server in _servers)
        {
            server.Validate();
            _logger.LogInformation("Processing server: {ServerProvider}...", server.Provider);
            
            // Resolve the latest token (potentially rotated)
            server.Token = await _tokenManagementService.GetCurrentTokenAsync(
                server.Provider, 
                server.EndPoint, 
                server.Token, 
                server.IsPrBot);

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
            repo.CloneUrl ?? throw new NullReferenceException($"The clone endpoint branch of {repo} is null!"),
            CloneMode.Full,
            $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}");

        // Run all plugins.
        await _runAllOfficialPluginsService.RunAllPlugins(workPath, true, onlyRunUpdatePlugin: connectionConfiguration.OnlyUpdate);

        // Run localization (nice to have, failures are acceptable).
        try
        {
            _logger.LogInformation("Starting localization for repository: {RepoName}...", repo.Name);
            await _localizationService.LocalizeProjectAsync(workPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Localization failed for repository: {RepoName}. Continuing anyway...", repo.Name);
        }

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
                "Auto dependencies upgrade by bot.",
                @"
Auto dependencies upgrade by bot. This is automatically generated by bot.

The bot tries to fetch all possible updates and modify the project files automatically.

This pull request may break or change the behavior of this application. Review with cautious!",
                connectionConfiguration.Token);
        else
            _logger.LogInformation("Skipped creating new pull request for {Repo} because there already exists", repo);
    }
}
