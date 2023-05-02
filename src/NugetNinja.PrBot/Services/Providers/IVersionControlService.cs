﻿namespace Aiursoft.NugetNinja.PrBot;

public interface IVersionControlService
{
    public string GetName();

    public Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken);

    public IAsyncEnumerable<Repository> GetMyStars(string endPoint, string userName, string patToken);

    public Task ForkRepo(string endPoint, string org, string repo, string patToken);

    public Task<List<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head, string patToken);

    public Task CreatePullRequest(string endPoint, string org, string repo, string head, string baseBranch, string patToken);

    public string GetPushPath(Server connectionConfiguration, Repository repo);
}
