namespace Aiursoft.NugetNinja.MergeBot.Models.Abstractions;

public interface IGitServer
{
    public string GetName();

    public Task<IReadOnlyCollection<MergeRequestSearchResult>> GetOpenMergeRequests(string endPoint,
        string userName, string patToken);
    
    public Task<DetailedMergeRequest> GetMergeRequestDetails(string endPoint, string userName, string patToken, int projectId, int mergeRequestId);
    
    public Task MergeRequest(string endPoint, string patToken, int projectId, int mergeRequestId);
}