using System.Text.Json.Serialization;
using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.GitServerBase.Services.Providers.GitLab;

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

    public async IAsyncEnumerable<Issue> GetAssignedIssues(string endPoint, string userName, string patToken)
    {
        logger.LogInformation("Listing all issues assigned to user: {UserName} in GitLab...", userName);
        for (var i = 1; ; i++)
        {
            var endpoint = $"{endPoint}/api/v4/issues?assignee_username={userName}&scope=all&state=opened&per_page=100&page={i}";
            var currentPageItems = await httpClient.SendHttpAndGetJson<List<GitLabIssue>>(endpoint, HttpMethod.Get, patToken);
            if (!currentPageItems.Any()) yield break;

            foreach (var issue in currentPageItems)
            {
                yield return new Issue
                {
                    Id = issue.Id,
                    Iid = issue.Iid,
                    ProjectId = issue.ProjectId,
                    Title = issue.Title,
                    Description = issue.Description,
                    State = issue.State,
                    WebUrl = issue.WebUrl,
                    Author = new User
                    {
                        Login = issue.Author?.UserName ?? throw new NullReferenceException("Issue author is null!")
                    }
                };
            }
        }
    }

    public async Task<bool> HasOpenMergeRequest(string endPoint, int projectId, int issueId, string patToken)
    {
        logger.LogInformation("Checking if issue #{IssueId} has open merge requests...", issueId);
        try
        {
            // Get project details to construct proper query
            var project = await GetProjectById(endPoint, projectId, patToken);
            if (project?.PathWithNameSpace == null)
            {
                logger.LogWarning("Could not get project details for project ID {ProjectId}", projectId);
                return false;
            }

            // Query merge requests for this project that are linked to this issue
            var endpoint = $"{endPoint}/api/v4/projects/{projectId}/merge_requests?state=opened&per_page=100";
            var mrs = await httpClient.SendHttpAndGetJson<List<GitLabPullRequest>>(endpoint, HttpMethod.Get, patToken);

            // Check if any MR references this issue in title or description
            foreach (var mr in mrs)
            {
                if (mr.Title?.Contains($"#{issueId}") == true || mr.Description?.Contains($"#{issueId}") == true)
                {
                    logger.LogInformation("Found open MR referencing issue #{IssueId}", issueId);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for merge requests for issue #{IssueId}", issueId);
            return false;
        }
    }

    private async Task<GitLabProject> GetProjectById(string endpoint, int projectId, string patToken)
    {
        return await httpClient.SendHttpAndGetJson<GitLabProject>($"{endpoint}/api/v4/projects/{projectId}", HttpMethod.Get, patToken);
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

    public async Task CreatePullRequest(string endPoint, string org, string repo, string head, string @base, string patToken)
    {
        var myName = head.Split(':').First();
        var myBranch = head.Split(":").Last();
        var project = await GetProject(endPoint, org, repo, patToken);
        logger.LogInformation("Creating a new pull request in GitLab: {Org}/{Repo}...", org, repo);
        var endpoint = $"{endPoint}/api/v4/projects/{myName}%2F{repo}/merge_requests";
        await httpClient.SendHttp(endpoint, HttpMethod.Post, patToken, new
        {
            title = "Auto dependencies upgrade by bot.",
            description = @"
Auto dependencies upgrade by bot. This is automatically generated by bot.

The bot tries to fetch all possible updates and modify the project files automatically.

This pull request may break or change the behavior of this application. Review with cautious!",
            source_branch = myBranch,
            target_branch = @base,
            target_project_id = project.Id
        });
    }

    private async Task<GitLabProject> GetProject(string endpoint, string org, string repo, string patToken)
    {
        //https://gitlab.aiursoft.com/api/v4/projects/aiursoft%2fscanner
        return await httpClient.SendHttpAndGetJson<GitLabProject>($"{endpoint}/api/v4/projects/{org}%2f{repo}", HttpMethod.Get, patToken);
    }

    public string GetPushPath(Server connectionConfiguration, Repository repo)
    {
        var pushPath = string.Format(connectionConfiguration.PushEndPoint,
                           $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}")
                       + $"/{connectionConfiguration.UserName}/{repo.Name}.git";
        return pushPath;
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
public class GitLabIssue
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("iid")]
    public int Iid { get; set; }

    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; set; }

    [JsonPropertyName("author")]
    public GitLabUser? Author { get; set; }
}
