using Aiursoft.Canon;
using Aiursoft.NugetNinja.MergeBot.Models.Abstractions;
using Aiursoft.NugetNinja.MergeBot.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.MergeBot;

public class Entry(
    RetryEngine retryEngine,
    ILogger<Entry> logger,
    IOptions<List<MergeServer>> servers,
    IEnumerable<IGitServer> gitServers)
{
    private readonly List<MergeServer> _servers = servers.Value;

    public async Task RunAsync()
    {
        foreach (var server in _servers)
        {
            var gitServer = gitServers.FirstOrDefault(s => s.GetName() == server.Provider);
            if (gitServer == null)
            {
                logger.LogError("Provider {Provider} is not supported!", server.Provider);
                continue;
            }
            
            logger.LogInformation("Checking open merge requests for {ServerProvider}...", server.Provider);
            var mergeRequests = await gitServer.GetOpenMergeRequests(server.EndPoint!, server.UserName!, server.Token!);
            var validMergeRequests = mergeRequests
                .Where(mr => mr.ProjectId != 0)
                .Where(mr => !mr.Draft)
                .Where(mr => !mr.WorkInProgress)
                .ToList();

            foreach (var mergeRequest in validMergeRequests)
            {
                logger.LogInformation("Found open merge request: {MergeRequest}", mergeRequest.Title);
                var details = await gitServer.GetMergeRequestDetails(server.EndPoint!, server.UserName!, server.Token!, mergeRequest.ProjectId, mergeRequest.IID);
                if (details.Pipeline?.Status != "success")
                {
                    logger.LogWarning("Merge request {MergeRequest} has a failed pipeline!", mergeRequest.Title);
                    continue;
                }
                
                logger.LogInformation("Merge request {MergeRequest} has a successful pipeline!", mergeRequest.Title);
                try
                {
                    await retryEngine.RunWithRetry(async (attempt) =>
                    {
                        logger.LogInformation("Merging merge request {MergeRequest} with attempt {Attempt}...",
                            mergeRequest.Title, attempt);
                        await gitServer.MergeRequest(server.EndPoint!, server.Token!, mergeRequest.ProjectId,
                            mergeRequest.IID);
                    }, attempts: 3);

                    logger.LogInformation("Merge request {MergeRequest} has been merged!", mergeRequest.Title);

                    await Task.Delay(5000);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to merge merge request {MergeRequest}!", mergeRequest.Title);
                }
            }
        }
    }
}