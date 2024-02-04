using Aiursoft.Canon;
using Aiursoft.NugetNinja.MergeBot.Models.Abstractions;
using Aiursoft.NugetNinja.MergeBot.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.MergeBot;

public class Entry
{
    private readonly RetryEngine _retryEngine;
    private readonly ILogger<Entry> _logger;
    private readonly IEnumerable<IGitServer> _gitServers;
    private readonly List<MergeServer> _servers;

    public Entry(
        RetryEngine retryEngine,
        ILogger<Entry> logger, 
        IOptions<List<MergeServer>> servers,
        IEnumerable<IGitServer> gitServers)
    {
        _retryEngine = retryEngine;
        _logger = logger;
        _gitServers = gitServers;
        _servers = servers.Value;
    }
    
    public async Task RunAsync()
    {
        foreach (var server in _servers)
        {
            var gitServer = _gitServers.FirstOrDefault(s => s.GetName() == server.Provider);
            if (gitServer == null)
            {
                _logger.LogError("Provider {Provider} is not supported!", server.Provider);
                continue;
            }
            
            _logger.LogInformation("Checking open merge requests for {ServerProvider}...", server.Provider);
            var mergeRequests = await gitServer.GetOpenMergeRequests(server.EndPoint!, server.UserName!, server.Token!);
            var validMergeRequests = mergeRequests
                .Where(mr => mr.ProjectId != 0)
                .Where(mr => !mr.Draft)
                .Where(mr => !mr.WorkInProgress)
                .ToList();

            foreach (var mergeRequest in validMergeRequests)
            {
                _logger.LogInformation("Found open merge request: {MergeRequest}", mergeRequest.Title);
                var details = await gitServer.GetMergeRequestDetails(server.EndPoint!, server.UserName!, server.Token!, mergeRequest.ProjectId, mergeRequest.IID);
                if (details.Pipeline?.Status != "success")
                {
                    _logger.LogWarning("Merge request {MergeRequest} has a failed pipeline!", mergeRequest.Title);
                    continue;
                }
                
                _logger.LogInformation("Merge request {MergeRequest} has a successful pipeline!", mergeRequest.Title);
                await _retryEngine.RunWithRetry(async (attempt) =>
                {
                    _logger.LogInformation("Merging merge request {MergeRequest} with attempt {Attempt}...", mergeRequest.Title, attempt);
                    await gitServer.MergeRequest(server.EndPoint!, server.Token!, mergeRequest.ProjectId, mergeRequest.IID);
                }, attempts: 3);
                    
                _logger.LogInformation("Merge request {MergeRequest} has been merged!", mergeRequest.Title);
            }
        }
    }
}