using Aiursoft.NugetNinja.GeminiBot.Services;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.GeminiBot;

/// <summary>
/// Application entry point and coordinator.
/// Orchestrates the high-level workflow without containing business logic.
/// </summary>
public class Entry
{
    private readonly List<Server> _servers;
    private readonly IEnumerable<IVersionControlService> _versionControls;
    private readonly IssueProcessor _issueProcessor;
    private readonly ILogger<Entry> _logger;

    public Entry(
        IOptions<List<Server>> servers,
        IEnumerable<IVersionControlService> versionControls,
        IssueProcessor issueProcessor,
        ILogger<Entry> logger)
    {
        _servers = servers?.Value ?? throw new ArgumentNullException(nameof(servers));
        _versionControls = versionControls ?? throw new ArgumentNullException(nameof(versionControls));
        _issueProcessor = issueProcessor ?? throw new ArgumentNullException(nameof(issueProcessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Gemini Bot for issue processing...");

        foreach (var server in _servers)
        {
            _logger.LogInformation("Processing server: {ServerProvider}...", server.Provider);

            var serviceProvider = GetVersionControlService(server.Provider);
            await ProcessServerAsync(server, serviceProvider);
        }
    }

    private IVersionControlService GetVersionControlService(string providerName)
    {
        var service = _versionControls.FirstOrDefault(v => v.GetName() == providerName);
        if (service == null)
        {
            throw new InvalidOperationException($"No version control service found for provider: {providerName}");
        }
        return service;
    }

    private async Task ProcessServerAsync(Server server, IVersionControlService versionControl)
    {
        var assignedIssues = await versionControl
            .GetAssignedIssues(server.EndPoint, server.UserName, server.Token)
            .ToListAsync();

        _logger.LogInformation("Got {IssuesCount} issues assigned to {UserName}", assignedIssues.Count, server.UserName);
        _logger.LogInformation("\n\n================================================================\n\n");

        foreach (var issue in assignedIssues)
        {
            await ProcessIssueAsync(issue, server);
        }
    }

    private async Task ProcessIssueAsync(Issue issue, Server server)
    {
        try
        {
            _logger.LogInformation("Processing issue: {Issue}...", issue);

            var result = await _issueProcessor.ProcessAsync(issue, server);

            if (result.Success)
            {
                _logger.LogInformation("Issue #{IssueId} processed: {Message}", issue.Iid, result.Message);
            }
            else
            {
                _logger.LogWarning("Issue #{IssueId} processing failed: {Message}", issue.Iid, result.Message);
                if (result.Error != null)
                {
                    _logger.LogError(result.Error, "Exception details for issue #{IssueId}", issue.Iid);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception when processing issue: {Issue}", issue);
        }
        finally
        {
            _logger.LogInformation("\n\n================================================================\n\n");
        }
    }
}
