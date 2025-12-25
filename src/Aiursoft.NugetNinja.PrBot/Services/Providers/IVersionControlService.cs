using Aiursoft.NugetNinja.PrBot.Models;

namespace Aiursoft.NugetNinja.PrBot.Services.Providers;

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
    public Task<bool> HasOpenPullRequestForIssue(string endPoint, int projectId, int issueId, string patToken);
    public Task<IEnumerable<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head,
        string patToken);
    public Task CreatePullRequest(string endPoint, string org, string repo, string head, string baseBranch,
        string title, string body, string patToken);

    // Helper methods
    public string GetPushPath(Server connectionConfiguration, Repository repo);
}