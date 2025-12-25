using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.PrBot.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PrBot.Services.Providers.Gitea;

public class GiteaService(
    HttpWrapper httpClient,
    ILogger<GiteaService> logger) : IVersionControlService
{
    public string GetName()
    {
        return "Gitea";
    }

    public async Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken)
    {
        logger.LogInformation("Getting if repository exists based on org: {OrgName}, repo: {RepoName}...", orgName, repoName);
        try
        {
            var endpoint = $@"{endPoint}/api/v1/repos/{orgName}/{repoName}";
            await httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async IAsyncEnumerable<Repository> GetMyStars(string endPoint, string userName, string patToken)
    {
        logger.LogInformation("Listing all stared repositories based on user\'s name: {UserName}...", userName);
        for (var i = 1; ; i++)
        {
            var endpoint = $@"{endPoint}/api/v1/users/{userName}/starred?page={i}";
            var currentPageItems = await httpClient.SendHttpAndGetJson<List<Repository>>(endpoint, HttpMethod.Get, patToken);
            if (!currentPageItems.Any()) yield break;

            foreach (var repo in currentPageItems) yield return repo;
        }
    }

    public async Task ForkRepo(string endPoint, string org, string repo, string patToken)
    {
        logger.LogInformation("Forking repository on Gitea with org: {Org}, repo: {Repo}...", org, repo);

        var endpoint = $@"{endPoint}/api/v1/repos/{org}/{repo}/forks";
        await httpClient.SendHttp(endpoint, HttpMethod.Post, patToken);
    }

    public async Task<IEnumerable<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head,
        string patToken)
    {
        logger.LogInformation("Getting pull requests on Gitea with org: {Org}, repo: {Repo}...", org, repo);

        var endpoint = $@"{endPoint}/api/v1/repos/{org}/{repo}/pulls?head={head}";
        return await httpClient.SendHttpAndGetJson<List<PullRequest>>(endpoint, HttpMethod.Get, patToken);
    }

    public async Task CreatePullRequest(string endPoint, string org, string repo, string head, string @base,
        string title, string body, string patToken)
    {
        logger.LogInformation("Creating a new pull request on Gitea with org: {Org}, repo: {Repo}...", org, repo);

        var endpoint = $@"{endPoint}/api/v1/repos/{org}/{repo}/pulls";
        await httpClient.SendHttp(endpoint, HttpMethod.Post, patToken, new
        {
            title,
            body,
            head,
            @base
        });
    }

    public async Task<Repository> GetRepository(string endPoint, string org, string repo, string patToken)
    {
        logger.LogInformation("Getting repository details for {Org}/{Repo} on Gitea...", org, repo);
        var endpoint = $@"{endPoint}/api/v1/repos/{org}/{repo}";
        var repository = await httpClient.SendHttpAndGetJson<Repository>(endpoint, HttpMethod.Get, patToken);
        if (repository == null) throw new InvalidOperationException($"Could not get repository details for {org}/{repo}");
        return repository;
    }

    public Task<bool> HasOpenPullRequestForIssue(string endPoint, int projectId, int issueId, string patToken)
    {
        logger.LogInformation("Checking for open PRs for issue #{IssueId} (not supported for Gitea)", issueId);
        return Task.FromResult(false);
    }

    public string GetPushPath(Server connectionConfiguration, Repository repo)
    {
        var pushPath = string.Format(connectionConfiguration.PushEndPoint, $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}") + $"/{connectionConfiguration.UserName}/{repo.Name}.git";
        return pushPath;
    }

    public Task<IReadOnlyCollection<GitServerBase.GitServerBase.Models.Abstractions.MergeRequestSearchResult>> GetOpenMergeRequests(string endPoint, string userName, string patToken)
    {
        throw new NotImplementedException("Merge requests are not supported for Gitea");
    }

    public Task<GitServerBase.GitServerBase.Models.Abstractions.DetailedMergeRequest> GetMergeRequestDetails(string endPoint, string userName, string patToken, int projectId, int mergeRequestId)
    {
        throw new NotImplementedException("Merge requests are not supported for Gitea");
    }

    public Task MergeRequest(string endPoint, string patToken, int projectId, int mergeRequestId)
    {
        throw new NotImplementedException("Merge requests are not supported for Gitea");
    }
}