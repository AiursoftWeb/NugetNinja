using Aiursoft.Canon;
using Aiursoft.NugetNinja.GitServerBase.Models.Configuration;
using Aiursoft.NugetNinja.GitServerBase.Services; // New using
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.MergeBot;

public class Entry(
    RetryEngine retryEngine,
    ILogger<Entry> logger,
    IOptions<List<MergeServer>> servers,
    IEnumerable<IVersionControlService> gitServers,
    TokenManagementService tokenManagementService)
{
    private readonly RetryEngine _retryEngine = retryEngine;
    private readonly ILogger<Entry> _logger = logger;
    private readonly List<MergeServer> _servers = servers.Value;
    private readonly IEnumerable<IVersionControlService> _gitServers = gitServers;
    private readonly TokenManagementService _tokenManagementService = tokenManagementService;

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Nuget Ninja Merge bot...");

        foreach (var server in _servers)
        {
            server.Validate();
            var gitServer = _gitServers.FirstOrDefault(s => s.GetName() == server.Provider);
            if (gitServer == null)
            {
                _logger.LogError("Provider {Provider} is not supported!", server.Provider);
                continue;
            }

            // Resolve the latest token (potentially rotated)
            server.Token = await _tokenManagementService.GetCurrentTokenAsync(
                server.Provider!, 
                server.EndPoint!, 
                server.Token!, 
                server.IsPrBot);

            _logger.LogInformation("Checking open merge requests for {ServerProvider}...", server.Provider);
            var mergeRequests = await gitServer.GetOpenMergeRequests(server.EndPoint!, server.UserName!, server.Token!);
            var validMergeRequests = mergeRequests
                .Where(mr => mr.ProjectId != 0)
                .Where(mr => !mr.Draft)
                .Where(mr => !mr.WorkInProgress)
                .ToList();

            foreach (var mergeRequest in validMergeRequests)
            {
                _logger.LogInformation("Found open merge request: {MergeRequest}, Id is: {Id}", mergeRequest.Title, mergeRequest.IID);
                var details = await gitServer.GetMergeRequestDetails(server.EndPoint!, server.UserName!, server.Token!, mergeRequest.ProjectId, mergeRequest.IID);
                if (details.Pipeline?.Status != "success")
                {
                    _logger.LogWarning("Merge request {MergeRequest} has a pipeline with status {Status}!", mergeRequest.Title, details.Pipeline?.Status);
                    continue;
                }

                _logger.LogInformation("Merge request {MergeRequest} has a successful pipeline!", mergeRequest.Title);
                try
                {
                    await _retryEngine.RunWithRetry(async (attempt) =>
                    {
                        _logger.LogInformation("Merging merge request {MergeRequest} with attempt {Attempt}...",
                            mergeRequest.Title, attempt);
                        await gitServer.MergeRequest(server.EndPoint!, server.Token!, mergeRequest.ProjectId,
                            mergeRequest.IID);
                    }, attempts: 3);

                    _logger.LogInformation("Merge request {MergeRequest} has been merged!", mergeRequest.Title);

                    await Task.Delay(5000);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to merge merge request {MergeRequest}!", mergeRequest.Title);
                }
            }
        }
    }
}
