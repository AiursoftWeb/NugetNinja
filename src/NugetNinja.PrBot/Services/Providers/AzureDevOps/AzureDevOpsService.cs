﻿using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Aiursoft.NugetNinja.PrBot;

public class AzureDevOpsService : IVersionControlService
{
    private readonly CacheService _cacheService;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(
        CacheService cacheService,
        ILogger<AzureDevOpsService> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    private Task<VssConnection> GetAzureDevOpsConnection(string endPoint, string patToken, bool allowCache = true)
    {
        return this._cacheService.RunWithCache($"azure-devops-client-{endPoint}-token-{patToken}", fallback: async () => 
        {
            var credentials = new VssBasicCredential(string.Empty, patToken);
            var connection = new VssConnection(new Uri(endPoint), credentials);
            await connection.ConnectAsync();
            return connection;
        }, cachedMinutes: allowCache ? 20 : 0);
    }

    private async IAsyncEnumerable<GitRepository> GetGitRepositories(string endPoint, string patToken)
    {
        var connection = await GetAzureDevOpsConnection(endPoint, patToken);
        var client = connection.GetClient<GitHttpClient>();
        var projectClient = connection.GetClient<ProjectHttpClient>();
        foreach (var project in await projectClient.GetProjects())
        {
            var repos = await client.GetRepositoriesAsync(project.Name);
            foreach(var repo in repos)
            {
                yield return repo;
            }
        }
    }

    public async Task CreatePullRequest(string endPoint, string org, string repo, string head, string baseBranch, string patToken)
    {
        await foreach(var azureDevOpsRepo in GetGitRepositories(endPoint, patToken))
        {
            if (azureDevOpsRepo.Name == repo)
            {
                var client = (await GetAzureDevOpsConnection(endPoint, patToken)).GetClient<GitHttpClient>();
                await client.CreatePullRequestAsync(new GitPullRequest
                {
                    Title = "Auto dependencies upgrade by bot.",
                    Description = @"
Auto dependencies upgrade by bot. This is automatically generated by bot.

The bot tries to fetch all possible updates and modify the project files automatically.

This pull request may break or change the behavior of this application. Review with cautious!",
                    // Hack here, because Azure DevOps is sooooo stupid that their developers have no idea about forking.
                    SourceRefName = @"refs/heads/" + head.Split(':').Last(),
                    TargetRefName = @"refs/heads/" + baseBranch,
                }, repositoryId: azureDevOpsRepo.Id);
                return;
            }
        }
    }

    public Task ForkRepo(string endPoint, string org, string repo, string patToken)
    {
        // Hack here, because Azure DevOps is sooooo stupid that their developers have no idea about forking.
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<Repository> GetMyStars(string endPoint, string userName, string patToken)
    {
        await foreach (var repo in GetGitRepositories(endPoint, patToken))
        {
            if (repo.DefaultBranch != null)
            {
                yield return new Repository
                {
                    Name = repo.Name,
                    FullName = repo.Name,
                    Archived = false,
                    Owner = new User
                    {
                        Login = repo.ProjectReference.Name
                    },
                    // Hack here, because Azure DevOps is sooooo stupid that don't support the standard clone grammar.
                    DefaultBranch = repo.DefaultBranch.Split('/').Last(),
                    CloneUrl = repo.SshUrl
                };
            }
            else
            {
                _logger.LogWarning($"Got a repository from Azure Devops with name: {repo.Name} who's default branch is null!");
            }
        }
    }

    public string GetName() => "AzureDevOps";

    public async Task<List<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head, string patToken)
    {
        await foreach (var azureDevOpsRepo in GetGitRepositories(endPoint, patToken))
        {
            if (azureDevOpsRepo.Name == repo)
            {
                var client = (await GetAzureDevOpsConnection(endPoint, patToken)).GetClient<GitHttpClient>();
                var prs = await client.GetPullRequestsAsync(azureDevOpsRepo.Id, new GitPullRequestSearchCriteria
                {
                    SourceRefName = @"refs/heads/" + head.Split(':').Last(),
                });
                return prs.Select(p => new PullRequest
                {
                    State = p.Status.ToString()
                }).ToList();
            }
        }
        throw new Exception($"Could not find Azure DevOps repo based on name: {repo}");
    }

    public Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken)
    {
        // This method is used for determining if to fork a repo.
        // Hack here, because Azure DevOps is sooooo stupid that their developers have no idea about forking.
        return Task.FromResult(true);
    }

    public string GetPushPath(Server connectionConfiguration, Repository repo)
    {
        // Hack here, because Azure DevOps is sooooo stupid that doesn't support pushing with HTTPS + PAT grammar.
        return repo.CloneUrl ?? throw new Exception($"Repo {repo}'s clone Url is null!");
    }
}
