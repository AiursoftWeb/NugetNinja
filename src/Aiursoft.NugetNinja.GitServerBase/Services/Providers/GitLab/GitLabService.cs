using System.Text.Json.Serialization;
using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Models.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

    public async Task<bool> HasOpenPullRequestForIssue(string endPoint, int projectId, int issueId, string patToken)
    {
        logger.LogInformation("Checking if issue #{IssueId} has open merge requests...", issueId);

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

    public async Task<Repository> GetRepository(string endPoint, string org, string repo, string patToken)
    {
        logger.LogInformation("Getting repository details for {Org}/{Repo} in GitLab...", org, repo);

        var project = await GetProject(endPoint, org, repo, patToken);

        if (project.HttpUrlToRepo == null || project.DefaultBranch == null || project.PathWithNameSpace == null)
        {
            throw new InvalidOperationException($"Could not get complete project details for {org}/{repo}");
        }

        return new Repository
        {
            Id = project.Id,
            Name = project.Path ?? throw new InvalidOperationException($"Project path is null for {org}/{repo}"),
            FullName = project.PathWithNameSpace,
            DefaultBranch = project.DefaultBranch,
            CloneUrl = project.HttpUrlToRepo,
            Archived = project.Archived,
            Owner = new User
            {
                Login = project.Namespace?.FullPath ?? throw new InvalidOperationException($"Project namespace is null for {org}/{repo}")
            }
        };
    }


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

    public async Task<IReadOnlyCollection<PipelineJob>> GetPipelineJobs(string endPoint, string patToken, int projectId, int pipelineId)
    {
        logger.LogTrace("Getting jobs for pipeline {PipelineId} in project {ProjectId}...", pipelineId, projectId);
        try
        {
            // First try the direct jobs endpoint
            var endpoint = $"{endPoint}/api/v4/projects/{projectId}/pipelines/{pipelineId}/jobs";
            logger.LogTrace("Trying endpoint: {Endpoint}", endpoint);
            var json = await httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
            var jobs = JsonConvert.DeserializeObject<List<PipelineJob>>(json);
            return jobs ?? new List<PipelineJob>();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            logger.LogWarning("Pipeline {PipelineId} jobs endpoint returned 404 in project {ProjectId}.", pipelineId, projectId);

            // Alternative: Try getting all jobs from the project and filter by pipeline
            try
            {
                logger.LogInformation("Trying alternative approach: fetching all project jobs and filtering...");
                var projectJobsEndpoint = $"{endPoint}/api/v4/projects/{projectId}/jobs?per_page=100";
                var allJobsJson = await httpClient.SendHttp(projectJobsEndpoint, HttpMethod.Get, patToken);
                var allJobs = JsonConvert.DeserializeObject<List<PipelineJob>>(allJobsJson) ?? new List<PipelineJob>();

                // Filter by pipeline ID
                var pipelineJobs = allJobs.Where(j => j.PipelineId == pipelineId).ToList();
                logger.LogInformation("Found {Count} jobs for pipeline {PipelineId} using alternative method", pipelineJobs.Count, pipelineId);
                return pipelineJobs;
            }
            catch (Exception alternativeEx)
            {
                logger.LogError(alternativeEx, "Alternative method also failed for pipeline {PipelineId}", pipelineId);
                return new List<PipelineJob>();
            }
        }
    }

    public async Task<string> GetJobLog(string endPoint, string patToken, int projectId, int jobId)
    {
        logger.LogTrace("Getting log for job {JobId} in GitLab...", jobId);
        var endpoint = $"{endPoint}/api/v4/projects/{projectId}/jobs/{jobId}/trace";
        var log = await httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
        return log;
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

    private async Task<GitLabProject> GetProject(string endpoint, string org, string repo, string patToken)
    {
        // GitLab API supports both:
        // - /api/v4/projects/{id} for numeric project IDs
        // - /api/v4/projects/{org}%2F{repo} for org/repo paths
        string projectIdentifier;
        if (string.IsNullOrEmpty(repo) && int.TryParse(org, out _))
        {
            // Using numeric project ID directly
            projectIdentifier = org;
        }
        else
        {
            // Using org/repo path format
            projectIdentifier = $"{org}%2f{repo}";
        }

        return await httpClient.SendHttpAndGetJson<GitLabProject>($"{endpoint}/api/v4/projects/{projectIdentifier}", HttpMethod.Get, patToken);
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
