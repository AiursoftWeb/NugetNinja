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
    TokenStoreService tokenStoreService) // Injected
{
    private readonly RetryEngine _retryEngine = retryEngine;
    private readonly ILogger<Entry> _logger = logger;
    private readonly List<MergeServer> _servers = servers.Value;
    private readonly IEnumerable<IVersionControlService> _gitServers = gitServers;
    private readonly TokenStoreService _tokenStoreService = tokenStoreService;

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting Nuget Ninja Merge bot...");

        var gitLabTokens = await _tokenStoreService.ReadTokensAsync() ?? new GitLabTokens();

        foreach (var server in _servers)
        {
            if (string.IsNullOrEmpty(server.Provider))
            {
                _logger.LogError("Server provider is null or empty!");
                continue;
            }

            var gitServer = _gitServers.FirstOrDefault(s => s.GetName() == server.Provider);
            if (gitServer == null)
            {
                _logger.LogError("Provider {Provider} is not supported!", server.Provider);
                continue;
            }

            if (server.Provider.Equals("GitLab", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(server.EndPoint) || string.IsNullOrEmpty(server.Token))
                {
                    _logger.LogError("Server EndPoint or Token is null or empty for provider: {Provider}", server.Provider);
                    continue;
                }

                var oldToken = server.Token;

                // Prioritize token from file if available
                if (!server.IsPrBot && !string.IsNullOrEmpty(gitLabTokens.MergeToken))
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
                    var newToken = await gitServer.RotateToken(server.EndPoint, oldToken);
                    server.Token = newToken; // Update the server object with the new token
                    gitLabTokens.MergeToken = newToken; // Store the new token
                    
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

            if (string.IsNullOrEmpty(server.EndPoint) || string.IsNullOrEmpty(server.UserName) || string.IsNullOrEmpty(server.Token))
            {
                _logger.LogError("Server EndPoint, UserName or Token is null or empty for provider: {Provider}", server.Provider);
                continue;
            }

            _logger.LogInformation("Checking open merge requests for {ServerProvider}...", server.Provider);
            var mergeRequests = await gitServer.GetOpenMergeRequests(server.EndPoint, server.UserName, server.Token);
            var validMergeRequests = mergeRequests
                .Where(mr => mr.ProjectId != 0)
                .Where(mr => !mr.Draft)
                .Where(mr => !mr.WorkInProgress)
                .ToList();

            foreach (var mergeRequest in validMergeRequests)
            {
                _logger.LogInformation("Found open merge request: {MergeRequest}, Id is: {Id}", mergeRequest.Title, mergeRequest.IID);
                var details = await gitServer.GetMergeRequestDetails(server.EndPoint, server.UserName, server.Token, mergeRequest.ProjectId, mergeRequest.IID);
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
                        await gitServer.MergeRequest(server.EndPoint, server.Token, mergeRequest.ProjectId,
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
