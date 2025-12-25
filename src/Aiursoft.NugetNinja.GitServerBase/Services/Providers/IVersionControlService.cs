using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Models.Abstractions;

namespace Aiursoft.NugetNinja.GitServerBase.Services.Providers;

public interface IVersionControlService
{
    public string GetName();

    // Repository operations
    public Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken);
    public Task<Repository> GetRepository(string endPoint, string org, string repo, string patToken);
    public Task ForkRepo(string endPoint, string org, string repo, string patToken);

    // Star management
    public IAsyncEnumerable<Repository> GetMyStars(string endPoint, string userName, string patToken);

    // Issue and Pull Request management
    public IAsyncEnumerable<Issue> GetAssignedIssues(string endPoint, string userName, string patToken);
    public Task<bool> HasOpenPullRequestForIssue(string endPoint, int projectId, int issueId, string patToken);
    public Task<IEnumerable<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head,
        string patToken);
    public Task CreatePullRequest(string endPoint, string org, string repo, string head, string baseBranch,
        string title, string body, string patToken);

    // Merge Request operations (for MergeBot)
    public Task<IReadOnlyCollection<MergeRequestSearchResult>> GetOpenMergeRequests(string endPoint, string userName, string patToken);
    public Task<DetailedMergeRequest> GetMergeRequestDetails(string endPoint, string userName, string patToken, int projectId, int mergeRequestId);
    public Task MergeRequest(string endPoint, string patToken, int projectId, int mergeRequestId);

    // Pipeline operations (for GeminiBot)
    public Task<IReadOnlyCollection<PipelineJob>> GetPipelineJobs(string endPoint, string patToken, int projectId, int pipelineId);
    public Task<string> GetJobLog(string endPoint, string patToken, int projectId, int jobId);

    // Helper methods
    public string GetPushPath(Server connectionConfiguration, Repository repo);
}