using System.Text.Json.Serialization;
using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.PrBot.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PrBot.Services.Providers.GitLab;

public class GitLabService(HttpWrapper httpClient, ILogger<GitLabService> logger) : IVersionControlService
{
    public string GetName() => "GitLab";

    public async Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken)
    {
        logger.LogInformation("Checking if repository exists in GitLab: {OrgName}/{RepoName}...", orgName, repoName);
        try
        {
            var endpoint = $"{endPoint}/api/v4/projects/{orgName}%2F{repoName}";
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
        logger.LogInformation("Listing all starred repositories for user: {UserName} in GitLab...", userName);
        for (var i = 1; ; i++)
        {
            var endpoint = $"{endPoint}/api/v4/users/{userName}/starred_projects?per_page=100&page={i}";
            var currentPageItems = await httpClient.SendHttpAndGetJson<List<GitLabProject>>(endpoint, HttpMethod.Get, patToken);
            if (!currentPageItems.Any()) yield break;

            foreach (var repo in currentPageItems) yield return new Repository
            {
                Id = repo.Id,
                Name = repo.Path,
                FullName = repo.PathWithNameSpace,
                Archived = repo.Archived,
                DefaultBranch = repo.DefaultBranch,
                CloneUrl = repo.HttpUrlToRepo,
                Owner = new User
                {
                    Login = repo.Namespace?.FullPath ?? throw new NullReferenceException("Got a GitLab repo with null namespace!")
                }
            };
        }
    }

    public async Task ForkRepo(string endPoint, string org, string repo, string patToken)
    {
        logger.LogInformation("Forking repository in GitLab: {Org}/{Repo}...", org, repo);
        var endpoint = $"{endPoint}/api/v4/projects/{org}%2F{repo}/fork";
        await httpClient.SendHttp(endpoint, HttpMethod.Post, patToken);
    }

    public async Task<IEnumerable<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head, string patToken)
    {
        logger.LogInformation("Getting pull requests in GitLab: {Org}/{Repo}...", org, repo);
        var endpoint = $"{endPoint}/api/v4/projects/{org}%2F{repo}/merge_requests?state=opened&source_branch={head.Split(':').Last()}";
        var gitlabPrs = await httpClient.SendHttpAndGetJson<List<GitLabPullRequest>>(endpoint, HttpMethod.Get, patToken);
        return gitlabPrs.Select(p => new PullRequest
        {
            User = new User
            {
                Login = p.Author?.UserName ?? throw new NullReferenceException("A pull request's author is null!")
            },
            State = p.State?.Replace("opened", "open")
        });
    }

    public async Task CreatePullRequest(string endPoint, string org, string repo, string head, string @base, string title, string body, string patToken)
    {
        var myName = head.Split(':').First();
        var myBranch = head.Split(":").Last();
        var project = await GetProject(endPoint, org, repo, patToken);
        logger.LogInformation("Creating a new pull request in GitLab: {Org}/{Repo}...", org, repo);
        var endpoint = $"{endPoint}/api/v4/projects/{myName}%2F{repo}/merge_requests";
        await httpClient.SendHttp(endpoint, HttpMethod.Post, patToken, new
        {
            title,
            description = body,
            source_branch = myBranch,
            target_branch = @base,
            target_project_id = project.Id
        });
    }

    public async Task<Repository> GetRepository(string endPoint, string org, string repo, string patToken)
    {
        logger.LogInformation("Getting repository details for {Org}/{Repo} in GitLab...", org, repo);
        var project = await GetProject(endPoint, org, repo, patToken);
        if (project.HttpUrlToRepo == null || project.DefaultBranch == null || project.PathWithNameSpace == null)
            throw new InvalidOperationException($"Could not get complete project details for {org}/{repo}");
        return new Repository
        {
            Id = project.Id,
            Name = project.Path ?? throw new InvalidOperationException($"Project path is null for {org}/{repo}"),
            FullName = project.PathWithNameSpace,
            DefaultBranch = project.DefaultBranch,
            CloneUrl = project.HttpUrlToRepo,
            Archived = project.Archived,
            Owner = new User { Login = project.Namespace?.FullPath ?? throw new InvalidOperationException($"Project namespace is null for {org}/{repo}") }
        };
    }

    public async Task<bool> HasOpenPullRequestForIssue(string endPoint, int projectId, int issueId, string patToken)
    {
        logger.LogInformation("Checking if issue #{IssueId} has open merge requests...", issueId);
        var endpoint = $"{endPoint}/api/v4/projects/{projectId}/merge_requests?state=opened&per_page=100";
        var mrs = await httpClient.SendHttpAndGetJson<List<GitLabPullRequest>>(endpoint, HttpMethod.Get, patToken);
        foreach (var mr in mrs)
            if (mr.Title?.Contains($"#{issueId}") == true || mr.Description?.Contains($"#{issueId}") == true)
            {
                logger.LogInformation("Found open MR referencing issue #{IssueId}", issueId);
                return true;
            }
        return false;
    }

    private async Task<GitLabProject> GetProject(string endpoint, string org, string repo, string patToken)
    {
        return await httpClient.SendHttpAndGetJson<GitLabProject>($"{endpoint}/api/v4/projects/{org}%2f{repo}", HttpMethod.Get, patToken);
    }

    public string GetPushPath(Server connectionConfiguration, Repository repo)
    {
        var pushPath = string.Format(connectionConfiguration.PushEndPoint, $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}") + $"/{connectionConfiguration.UserName}/{repo.Name}.git";
        return pushPath;
    }

    public async Task<IReadOnlyCollection<GitServerBase.GitServerBase.Models.Abstractions.MergeRequestSearchResult>> GetOpenMergeRequests(string endPoint, string userName, string patToken)
    {
        logger.LogTrace("Listing all open merge requests for user: {UserName} in GitLab...", userName);
        var endpoint = $"{endPoint}/api/v4/merge_requests?state=opened&author_username={userName}&scope=all&per_page=100";
        var json = await httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
        var mergeRequests = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GitServerBase.GitServerBase.Models.Abstractions.MergeRequestSearchResult>>(json);
        return mergeRequests!;
    }

    public async Task<GitServerBase.GitServerBase.Models.Abstractions.DetailedMergeRequest> GetMergeRequestDetails(string endPoint, string userName, string patToken, int projectId, int mergeRequestId)
    {
        logger.LogTrace("Getting details for merge request {MergeRequestId} in GitLab...", mergeRequestId);
        var endpoint = $"{endPoint}/api/v4/projects/{projectId}/merge_requests/{mergeRequestId}";
        var json = await httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
        var mergeRequest = Newtonsoft.Json.JsonConvert.DeserializeObject<GitServerBase.GitServerBase.Models.Abstractions.DetailedMergeRequest>(json);
        return mergeRequest!;
    }

    public async Task MergeRequest(string endPoint, string patToken, int projectId, int mergeRequestId)
    {
        logger.LogInformation("Merging merge request {MergeRequestId} in GitLab...", mergeRequestId);
        var endpoint = $"{endPoint}/api/v4/projects/{projectId}/merge_requests/{mergeRequestId}/merge";
        await httpClient.SendHttp(endpoint, HttpMethod.Put, patToken);
    }
}

public class GitLabNamespace
{
    [JsonPropertyName("full_path")]
    public string? FullPath { get; set; }
}

public class GitLabProject
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("path_with_namespace")]
    public string? PathWithNameSpace { get; set; }

    [JsonPropertyName("http_url_to_repo")]
    public string? HttpUrlToRepo { get; set; }

    [JsonPropertyName("default_branch")]
    public string? DefaultBranch { get; set; }

    [JsonPropertyName("namespace")]
    public GitLabNamespace? Namespace { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }
}

public class GitLabPullRequest
{
    [JsonPropertyName("author")]
    public GitLabUser? Author { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class GitLabUser
{
    [JsonPropertyName("username")]
    public string? UserName { get; set; }
}