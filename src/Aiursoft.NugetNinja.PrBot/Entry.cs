using System.Net;
using Aiursoft.Canon;
using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Models;
using Aiursoft.CSTools.Services;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Services; // New using statement
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;
using Aiursoft.NugetNinja.PrBot.Configuration; // New using statement
using Aiursoft.NugetNinja.PrBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.PrBot;

public class Entry(
    RetryEngine retryEngine,
    IOptions<List<Server>> servers,
    IEnumerable<IVersionControlService> versionControls,
    RunAllOfficialPluginsService runAllOfficialPluginsService,
    WorkspaceManager workspaceManager,
    CommandService commands,
    Extractor extractor,
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
            var variable = server.HttpTokenEnvironmentVariable;
            var originVariable = variable + "_ORIGIN";
            var previousOrigin = string.IsNullOrWhiteSpace(variable) ? null : Environment.GetEnvironmentVariable(originVariable);
            var previousValue = string.IsNullOrWhiteSpace(variable) ? null : Environment.GetEnvironmentVariable(variable);
            try
            {
                if (!string.IsNullOrWhiteSpace(variable))
                {
                    // Export the current token after rotation, rather than the stale startup value.
                    var endpoint = new Uri(server.EndPoint, UriKind.Absolute);
                    if (endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo))
                    {
                        throw new InvalidOperationException("HTTP token export requires an HTTPS server endpoint without credentials.");
                    }
                    Environment.SetEnvironmentVariable(originVariable, endpoint.GetLeftPart(UriPartial.Authority));
                    Environment.SetEnvironmentVariable(variable, server.Token);
                }
                await RunServerAsync(server, serviceProvider);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(variable))
                {
                    Environment.SetEnvironmentVariable(variable, previousValue);
                    Environment.SetEnvironmentVariable(originVariable, previousOrigin);
                }
            }
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

        var workPath = Path.Combine(_workspaceFolder, $"{repo.Id}-{repo.Name}");
        var proposalStore = new ProposalCooldownStore(_workspaceFolder,
            $"{connectionConfiguration.EndPoint}\n{connectionConfiguration.UserName}\n{repo.Id}");
        using var proposalLock = proposalStore.Lock();

        _logger.LogInformation("Cloning repository: {RepoName} to {Path}...", repo.Name, workPath);
        await _workspaceManager.ResetRepo(
            workPath,
            repo.DefaultBranch ?? throw new NullReferenceException($"The default branch of {repo} is null!"),
            repo.CloneUrl ?? throw new NullReferenceException($"The clone endpoint of {repo} is null!"),
            CloneMode.Full,
            $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}");

        var cooldowns = (await extractor.ReadConfigAsync(workPath)).ProposalCooldowns;
        if (cooldowns is not null)
        {
            await ProcessWithCooldowns(repo, connectionConfiguration, versionControl, workPath, cooldowns, proposalStore);
            return;
        }

        await _runAllOfficialPluginsService.RunAllPlugins(workPath, true, onlyRunUpdatePlugin: connectionConfiguration.OnlyUpdate);
        await Localize(repo, workPath);

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

        await EnsureFork(repo, connectionConfiguration, versionControl);
        await _workspaceManager.Push(
            workPath,
            connectionConfiguration.ContributionBranch,
            versionControl.GetPushPath(connectionConfiguration, repo),
            true);
        await EnsurePullRequest(repo, connectionConfiguration, versionControl);
    }

    private async Task ProcessWithCooldowns(
        Repository repo, Server server, IVersionControlService versionControl, string workPath,
        NinjaProposalCooldowns cooldowns, ProposalCooldownStore store)
    {
        var publication = store.Load();
        var branch = new ProposalBranch(commands, workPath, server.ContributionBranch, versionControl.GetPushPath(server, repo));
        var forkExists = await versionControl.RepoExists(server.EndPoint, server.UserName, repo.Name!, server.Token);
        var remoteRevision = forkExists ? await branch.ReadRemoteRevision() : null;

        if (publication.Pending is not null)
        {
            publication.Recover(remoteRevision);
            store.Save(publication);
            if (publication.Pending is { Pushed: true } pending && remoteRevision == pending.Revision)
            {
                await EnsurePullRequest(repo, server, versionControl);
            }

            publication.Pending = null;
            store.Save(publication);
        }

        if (remoteRevision is not null && await HasOpenPullRequest(repo, server, versionControl))
        {
            await branch.PreserveExistingProposal(remoteRevision);
        }

        var priorTree = await branch.Snapshot();
        var roundTime = DateTimeOffset.UtcNow;
        var canUpgradeNuget = publication.IsReady(ProposalCategory.Nuget, cooldowns.NugetInterval, roundTime);
        var canUpgradeNpm = publication.IsReady(ProposalCategory.Npm, cooldowns.NpmInterval, roundTime);
        var changed = new HashSet<ProposalCategory>();
        await _runAllOfficialPluginsService.RunAllPlugins(workPath, true, server.OnlyUpdate, IncludeAction);

        if (publication.IsReady(ProposalCategory.Translation, cooldowns.TranslationInterval, roundTime))
        {
            var beforeTranslation = await branch.Snapshot();
            await Localize(repo, workPath);
            if (await branch.Snapshot() != beforeTranslation)
            {
                changed.Add(ProposalCategory.Translation);
            }
        }

        if (await branch.Snapshot() == priorTree)
        {
            _logger.LogInformation("No new proposal changes for {Repo}", repo);
            return;
        }

        await _workspaceManager.SetUserConfig(workPath, server.DisplayName, server.UserEmail);
        if (!await _workspaceManager.CommitToBranch(workPath, "Auto csproj fix and update by bot.", server.ContributionBranch))
        {
            return;
        }

        if (!forkExists)
        {
            await EnsureFork(repo, server, versionControl);
        }

        publication.Pending = new PendingProposal(await branch.CurrentRevision(), remoteRevision,
            DateTimeOffset.UtcNow, [.. changed]);
        store.Save(publication);
        await branch.Push(remoteRevision);
        publication.ConfirmPush(DateTimeOffset.UtcNow);
        store.Save(publication);
        await EnsurePullRequest(repo, server, versionControl);
        publication.Pending = null;
        store.Save(publication);

        bool IncludeAction(IAction action)
        {
            switch (action)
            {
                case NpmPackageUpgrade when canUpgradeNpm:
                    changed.Add(ProposalCategory.Npm);
                    return true;
                case NpmPackageUpgrade:
                    _logger.LogInformation("Deferring npm upgrade for {Repo} until its proposal cooldown elapses", repo);
                    return false;
                case PossiblePackageUpgrade or PackageGroupUpgrade when canUpgradeNuget:
                    changed.Add(ProposalCategory.Nuget);
                    return true;
                case PossiblePackageUpgrade or PackageGroupUpgrade:
                    _logger.LogInformation("Deferring NuGet upgrade for {Repo} until its proposal cooldown elapses", repo);
                    return false;
                default:
                    return true;
            }
        }
    }

    private async Task Localize(Repository repo, string workPath)
    {
        try
        {
            await _localizationService.LocalizeProjectAsync(workPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Localization failed for repository: {RepoName}. Continuing anyway...", repo.Name);
        }
    }

    private async Task EnsureFork(Repository repo, Server server, IVersionControlService versionControl)
    {
        if (await versionControl.RepoExists(server.EndPoint, server.UserName, repo.Name!, server.Token))
        {
            return;
        }

        await versionControl.ForkRepo(server.EndPoint, repo.Owner!.Login!, repo.Name!, server.Token);
        do
        {
            await Task.Delay(5000);
        } while (!await versionControl.RepoExists(server.EndPoint, server.UserName, repo.Name!, server.Token));
    }

    private async Task<bool> HasOpenPullRequest(Repository repo, Server server, IVersionControlService versionControl)
    {
        var pullRequests = await versionControl.GetPullRequests(
            server.EndPoint, repo.Owner!.Login!, repo.Name!, $"{server.UserName}:{server.ContributionBranch}", server.Token);
        return pullRequests.Any(p => p.State == "open" &&
            string.Equals(p.User?.Login, server.UserName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsurePullRequest(Repository repo, Server connectionConfiguration,
        IVersionControlService versionControl)
    {
        await retryEngine.RunWithRetry(async attempt =>
        {
            if (attempt > 1)
            {
                _logger.LogWarning("Retrying pull request creation for {Repo}, attempt {Attempt} of 3...", repo, attempt);
                // GitLab may briefly report a pushed branch as missing while its branch cache catches up.
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            if (!await HasOpenPullRequest(repo, connectionConfiguration, versionControl))
                // Create a new pull request.
                await versionControl.CreatePullRequest(
                    connectionConfiguration.EndPoint,
                    repo.Owner!.Login!,
                    repo.Name!,
                    $"{connectionConfiguration.UserName}:{connectionConfiguration.ContributionBranch}",
                    repo.DefaultBranch!,
                    "Auto dependencies upgrade by bot.",
                    @"
Auto dependencies upgrade by bot. This is automatically generated by bot.

The bot tries to fetch all possible updates and modify the project files automatically.

This pull request may break or change the behavior of this application. Review with cautious!",
                    connectionConfiguration.Token);
            else
                _logger.LogInformation("Skipped creating new pull request for {Repo} because there already exists", repo);
        }, attempts: 3, when: exception => exception is HttpRequestException httpException &&
            (httpException.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
             (int)httpException.StatusCode >= 500 ||
             (connectionConfiguration.Provider == "GitLab" && httpException.StatusCode == HttpStatusCode.BadRequest)));
    }
}
