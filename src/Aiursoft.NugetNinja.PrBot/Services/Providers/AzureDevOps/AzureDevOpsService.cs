using Aiursoft.Canon;
using Aiursoft.NugetNinja.PrBot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Aiursoft.NugetNinja.PrBot.Services.Providers.AzureDevOps;

public class AzureDevOpsService(
    CacheService cacheService,
    ILogger<AzureDevOpsService> logger)
    : IVersionControlService
{
    public async Task CreatePullRequest(string endPoint, string org, string repo, string head, string baseBranch,
        string title, string body, string patToken)
    {
        await foreach (var azureDevOpsRepo in GetGitRepositories(endPoint, patToken))
            if (azureDevOpsRepo.Name == repo)
            {
                var client = (await GetAzureDevOpsConnection(endPoint, patToken)).GetClient<GitHttpClient>();
                await client.CreatePullRequestAsync(new GitPullRequest
                {
                    Title = title,
                    Description = body,
                    // Hack here, because Azure DevOps is so stupid that their developers have no idea about forking.
                    SourceRefName = @"refs/heads/" + head.Split(':').Last(),
                    TargetRefName = @"refs/heads/" + baseBranch
                }, azureDevOpsRepo.Id);
                return;
            }
    }

    public Task ForkRepo(string endPoint, string org, string repo, string patToken)
    {
        // Hack here, because Azure DevOps is so stupid that their developers have no idea about forking.
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<Repository> GetMyStars(string endPoint, string userName, string patToken)
    {
        await foreach (var repo in GetGitRepositories(endPoint, patToken))
            if (repo.DefaultBranch != null)
                yield return new Repository
                {
                    Name = repo.Name,
                    FullName = repo.Name,
                    Owner = new User
                    {
                        Login = repo.ProjectReference.Name
                    },
                    // Hack here, because Azure DevOps is so stupid that don't support the standard clone grammar.
                    DefaultBranch = repo.DefaultBranch.Split('/').Last(),
                    CloneUrl = repo.SshUrl
                };
            else
                logger.LogWarning("Got a repository from Azure Devops with name: {RepoName} who\'s default branch is null!", repo.Name);
    }

    public string GetName()
    {
        return "AzureDevOps";
    }

    public async Task<IEnumerable<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head,
        string patToken)
    {
        await foreach (var azureDevOpsRepo in GetGitRepositories(endPoint, patToken))
            if (azureDevOpsRepo.Name == repo)
            {
                var client = (await GetAzureDevOpsConnection(endPoint, patToken)).GetClient<GitHttpClient>();
                var prs = await client.GetPullRequestsAsync(azureDevOpsRepo.Id, new GitPullRequestSearchCriteria
                {
                    SourceRefName = @"refs/heads/" + head.Split(':').Last()
                });
                return prs.Select(p => new PullRequest
                {
                    State = p.Status.ToString()
                }).ToList();
            }

        throw new Exception($"Could not find Azure DevOps repo based on name: {repo}");
    }

    public Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken)
    {
        // This method is used for determining if to fork a repo.
        // Hack here, because Azure DevOps is so stupid that their developers have no idea about forking.
        return Task.FromResult(true);
    }

    public async Task<Repository> GetRepository(string endPoint, string org, string repo, string patToken)
    {
        logger.LogInformation("Getting repository details for {Repo} on Azure DevOps...", repo);
        await foreach (var azureDevOpsRepo in GetGitRepositories(endPoint, patToken))
            if (azureDevOpsRepo.Name == repo)
            {
                if (azureDevOpsRepo.DefaultBranch == null) throw new InvalidOperationException($"Repository {repo}'s default branch is null!");
                return new Repository
                {
                    Name = azureDevOpsRepo.Name,
                    FullName = azureDevOpsRepo.Name,
                    Owner = new User { Login = azureDevOpsRepo.ProjectReference.Name },
                    DefaultBranch = azureDevOpsRepo.DefaultBranch.Split('/').Last(),
                    CloneUrl = azureDevOpsRepo.SshUrl
                };
            }
        throw new InvalidOperationException($"Could not find Azure DevOps repository: {repo}");
    }

    public Task<bool> HasOpenPullRequestForIssue(string endPoint, int projectId, int issueId, string patToken)
    {
        logger.LogInformation("Checking for open PRs for issue #{IssueId} (not supported for Azure DevOps)", issueId);
        return Task.FromResult(false);
    }

    public string GetPushPath(Server connectionConfiguration, Repository repo)
    {
        // Hack here, because Azure DevOps is so stupid that doesn't support pushing with HTTPS + PAT grammar.
        return repo.CloneUrl ?? throw new Exception($"Repo {repo}'s clone Url is null!");
    }

    public Task<IReadOnlyCollection<GitServerBase.GitServerBase.Models.Abstractions.MergeRequestSearchResult>> GetOpenMergeRequests(string endPoint, string userName, string patToken)
    {
        throw new NotImplementedException("Merge requests are not supported for Azure DevOps");
    }

    public Task<GitServerBase.GitServerBase.Models.Abstractions.DetailedMergeRequest> GetMergeRequestDetails(string endPoint, string userName, string patToken, int projectId, int mergeRequestId)
    {
        throw new NotImplementedException("Merge requests are not supported for Azure DevOps");
    }

    public Task MergeRequest(string endPoint, string patToken, int projectId, int mergeRequestId)
    {
        throw new NotImplementedException("Merge requests are not supported for Azure DevOps");
    }

    private async Task<VssConnection> GetAzureDevOpsConnection(string endPoint, string patToken)
    {
        return await cacheService.RunWithCache($"azure-devops-client-{endPoint}-token-{patToken}", async () =>
        {
            var credentials = new VssBasicCredential(string.Empty, patToken);
            var connection = new VssConnection(new Uri(endPoint), credentials);
            await connection.ConnectAsync();
            return connection;
        }) ?? throw new InvalidOperationException($"Failed to connect to Azure devops server: {endPoint}");
    }

    private async IAsyncEnumerable<GitRepository> GetGitRepositories(string endPoint, string patToken)
    {
        var connection = await GetAzureDevOpsConnection(endPoint, patToken);
        var client = connection.GetClient<GitHttpClient>();
        var projectClient = connection.GetClient<ProjectHttpClient>();
        foreach (var project in await projectClient.GetProjects())
        {
            var repos = await client.GetRepositoriesAsync(project.Name);
            foreach (var repo in repos) yield return repo;
        }
    }
}