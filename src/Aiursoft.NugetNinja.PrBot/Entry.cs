using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Models;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Models.Configuration; // New using statement
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
    TokenStoreService tokenStoreService, // Injected
    IOptions<PrBotOptions> prBotOptions, // Injected
    ILogger<Entry> logger)
{
    private readonly List<Server> _servers = servers.Value;
    private readonly TokenStoreService _tokenStoreService = tokenStoreService;
    private readonly string _workspaceFolder = prBotOptions.Value.WorkspacePath; // Initialized from options
    private readonly ILogger<Entry> _logger = logger;
    private readonly IEnumerable<IVersionControlService> _versionControls = versionControls;
    private readonly RunAllOfficialPluginsService _runAllOfficialPluginsService = runAllOfficialPluginsService;
    private readonly WorkspaceManager _workspaceManager = workspaceManager;
    private readonly LocalizationService _localizationService = localizationService;

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Nuget Ninja PR bot...");

        var gitLabTokens = await _tokenStoreService.ReadTokensAsync() ?? new GitLabTokens();

        foreach (var server in _servers)
        {
            _logger.LogInformation("Processing server: {ServerProvider}...", server.Provider);
            var serviceProvider = _versionControls.First(v => v.GetName() == server.Provider);

            if (server.Provider.Equals("GitLab", StringComparison.OrdinalIgnoreCase))
            {
                var oldToken = server.Token;

                // Prioritize token from file if available
                if (server.IsPrBot && !string.IsNullOrEmpty(gitLabTokens.ContributeToken))
                {
                    oldToken = gitLabTokens.ContributeToken;
                    _logger.LogInformation("Using GitLab contribute token from file for server: {EndPoint}", server.EndPoint);
                }
                else if (!server.IsPrBot && !string.IsNullOrEmpty(gitLabTokens.MergeToken))
                {
                    oldToken = gitLabTokens.MergeToken;
                    _logger.LogInformation("Using GitLab merge token from file for server: {EndPoint}", server.EndPoint);
                }
                else
                {
                    _logger.LogInformation("Using GitLab token from environment variable for server: {EndPoint}", server.EndPoint);
                }

                try
                {
                    var newToken = await serviceProvider.RotateToken(server.EndPoint, oldToken);
                    server.Token = newToken; // Update the server object with the new token

                    // Store the new token in the gitLabTokens object
                    if (server.IsPrBot)
                    {
                        gitLabTokens.ContributeToken = newToken;
                    }
                    else
                    {
                        gitLabTokens.MergeToken = newToken;
                    }

                    // Save immediately!
                    await _tokenStoreService.SaveTokensAsync(gitLabTokens);
                    _logger.LogInformation("New GitLab token saved to disk strictly.");
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Failed to rotate GitLab token for server: {EndPoint}. Crashing application.", server.EndPoint);
                    throw; // Crash the application as per requirement
                }
            }
            else
            {
                _logger.LogInformation("Token rotation not implemented for provider: {Provider}", server.Provider);
            }
            
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
