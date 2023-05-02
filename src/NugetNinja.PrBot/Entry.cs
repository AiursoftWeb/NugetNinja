using Microsoft.Extensions.Logging;
using Aiursoft.NugetNinja.AllOfficialsPlugin;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.PrBot;

public class Entry
{
    private readonly string _workspaceFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NugetNinjaWorkspace");
    private readonly List<Server> _servers;
    private readonly IEnumerable<IVersionControlService> _versionControls;
    private readonly RunAllOfficialPluginsService _runAllOfficialPluginsService;
    private readonly WorkspaceManager _workspaceManager;
    private readonly ILogger<Entry> _logger;

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

        foreach (var server in this._servers)
        {
            _logger.LogInformation($"Processing server: {server.Provider}...");
            var serviceProvider = _versionControls.First(v => v.GetName() == server.Provider);
            await this.RunServerAsync(server, serviceProvider);
        }
    }

    public async Task RunServerAsync(Server server, IVersionControlService versionControl)
    {
        var myStars = await versionControl
            .GetMyStars(server.EndPoint, server.UserName, server.Token)
            .Where(r => r.Archived == false)
            .Where(r => r.Owner?.Login != server.UserName)
            .ToListAsync();

        _logger.LogInformation($"Got {myStars.Count} stared repositories as registered to create pull requests automatically.");
        _logger.LogInformation("\r\n\r\n");
        _logger.LogInformation("================================================================");
        _logger.LogInformation("\r\n\r\n");
        foreach (var repo in myStars)
        {
            try
            {
                _logger.LogInformation($"Processing repository {repo.FullName}...");
                await ProcessRepository(repo, server, versionControl);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Crashed when processing repo: {repo.FullName}!");
            }
            finally
            {
                _logger.LogInformation("\r\n\r\n");
                _logger.LogInformation("================================================================");
                _logger.LogInformation("\r\n\r\n");
            }
        }
    }

    private async Task ProcessRepository(Repository repo, Server connectionConfiguration, IVersionControlService versionControl)
    {
        if (string.IsNullOrWhiteSpace(repo.Owner?.Login) || string.IsNullOrWhiteSpace(repo.Name))
        {
            throw new InvalidDataException($"The repo with path: {repo.FullName} is having invalid data!");
        }

        // Clone locally.
        var workPath = Path.Combine(_workspaceFolder, $"{repo.Id}-{repo.Name}");
        _logger.LogInformation($"Cloning repository: {repo.Name} to {workPath}...");
        await _workspaceManager.ResetRepo(
            path: workPath,
            branch: repo.DefaultBranch ?? throw new NullReferenceException($"The default branch of {repo.Name} is null!"),
            endPoint: repo.CloneUrl ?? throw new NullReferenceException($"The clone endpoint branch of {repo.Name} is null!"));

        // Run all plugins.
        await _runAllOfficialPluginsService.OnServiceStartedAsync(workPath, true);

        // Consider changes...
        if (!await _workspaceManager.PendingCommit(workPath))
        {
            _logger.LogInformation($"{repo} has no suggestion that we can make. Ignore.");
            return;
        }
        _logger.LogInformation($"{repo} is pending some fix. We will try to create\\update related pull request.");
        await _workspaceManager.SetUserConfig(workPath, username: connectionConfiguration.DisplayName, email: connectionConfiguration.UserEmail);
        var saved = await _workspaceManager.CommitToBranch(workPath, "Auto csproj fix and update by bot.", branch: connectionConfiguration.ContributionBranch);
        if (!saved)
        {
            _logger.LogInformation($"{repo} has no suggestion that we can make. Ignore.");
            return;
        }

        // Fork repo.
        if (!await versionControl.RepoExists(
            endPoint: connectionConfiguration.EndPoint, 
            connectionConfiguration.UserName, 
            repo.Name, 
            patToken: connectionConfiguration.Token))
        {
            await versionControl.ForkRepo(
                endPoint: connectionConfiguration.EndPoint,
                org: repo.Owner.Login,
                repo: repo.Name,
                patToken: connectionConfiguration.Token);
            await Task.Delay(5000);
            while (!await versionControl.RepoExists(
                endPoint: connectionConfiguration.EndPoint,
                orgName: connectionConfiguration.UserName, 
                repoName: repo.Name,
                patToken: connectionConfiguration.Token))
            {
                // Wait a while. GitHub may need some time to fork the repo.
                await Task.Delay(5000);
            }
        }

        // Push to forked repo.
        var pushPath = versionControl.GetPushPath(connectionConfiguration, repo);
            
        await _workspaceManager.Push(
            sourcePath: workPath,
            branch: connectionConfiguration.ContributionBranch,
            endpoint: pushPath,
            force: true);

        var existingPullRequestsByBot = (await versionControl.GetPullRequests(
            endPoint: connectionConfiguration.EndPoint,
            org: repo.Owner.Login,
            repo: repo.Name,
            head: $"{connectionConfiguration.UserName}:{connectionConfiguration.ContributionBranch}",
            patToken: connectionConfiguration.Token))
            .Where(p => string.Equals(p.User?.Login, connectionConfiguration.UserName, StringComparison.OrdinalIgnoreCase));

        if (existingPullRequestsByBot.All(p => p.State != "open"))
        {
            // Create a new pull request.
            await versionControl.CreatePullRequest(
                endPoint: connectionConfiguration.EndPoint,
                org: repo.Owner.Login,
                repo: repo.Name,
                head: $"{connectionConfiguration.UserName}:{connectionConfiguration.ContributionBranch}",
                baseBranch: repo.DefaultBranch,
                patToken: connectionConfiguration.Token);
        }
        else
        {
            _logger.LogInformation($"Skipped creating new pull request for {repo} because there already exists.");
        }
    }
}
