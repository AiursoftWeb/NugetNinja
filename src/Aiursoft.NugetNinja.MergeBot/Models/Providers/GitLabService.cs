using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.MergeBot.Models.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Aiursoft.NugetNinja.MergeBot.Models.Providers;

public class GitLabService(HttpWrapper httpClient, ILogger<GitLabService> logger) : IGitServer
{
    public string GetName() => "GitLab";

    
    public async Task<IReadOnlyCollection<MergeRequestSearchResult>> GetOpenMergeRequests(string endPoint, string userName, string patToken)
    {
        logger.LogTrace("Listing all open merge requests for user: {UserName} in GitLab...", userName);
        var endpoint = $"{endPoint}/api/v4/merge_requests?state=opened&author_username={userName}&scope=all&per_page=100";
        var json = await httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
        var mergeRequests = JsonConvert.DeserializeObject<List<MergeRequestSearchResult>>(json);
        return mergeRequests!;
    }
    
    public async Task<DetailedMergeRequest> GetMergeRequestDetails(string endPoint, string userName, string patToken, int projectId, int mergeRequestId)
    {
        logger.LogTrace("Getting details for merge request {MergeRequestId} in GitLab...", mergeRequestId);
        var endpoint = $"{endPoint}/api/v4/projects/{projectId}/merge_requests/{mergeRequestId}";
        var json = await httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
        var mergeRequest = JsonConvert.DeserializeObject<DetailedMergeRequest>(json);
        return mergeRequest!;
    }
    
    public async Task MergeRequest(string endPoint, string patToken, int projectId, int mergeRequestId)
    {
        logger.LogInformation("Merging merge request {MergeRequestId} in GitLab...", mergeRequestId);
        var endpoint = $"{endPoint}/api/v4/projects/{projectId}/merge_requests/{mergeRequestId}/merge";
        await httpClient.SendHttp(endpoint, HttpMethod.Put, patToken);
    }
}