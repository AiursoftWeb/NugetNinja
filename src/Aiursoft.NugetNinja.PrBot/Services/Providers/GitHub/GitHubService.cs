﻿using Aiursoft.NugetNinja.PrBot.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PrBot.Services.Providers.GitHub;

public class GitHubService : IVersionControlService
{
    private readonly HttpWrapper _httpClient;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(
        HttpWrapper httpClient,
        ILogger<GitHubService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string GetName()
    {
        return "GitHub";
    }

    public async Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken)
    {
        _logger.LogInformation("Getting if repository exists based on org: {OrgName}, repo: {RepoName}...", orgName, repoName);
        try
        {
            var endpoint = $@"{endPoint}/repos/{orgName}/{repoName}";
            await _httpClient.SendHttp(endpoint, HttpMethod.Get, patToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async IAsyncEnumerable<Repository> GetMyStars(string endPoint, string userName, string patToken)
    {
        _logger.LogInformation("Listing all stared repositories based on user\'s name: {UserName}...", userName);
        for (var i = 1;; i++)
        {
            var endpoint = $@"{endPoint}/users/{userName}/starred?page={i}";
            var currentPageItems = await _httpClient.SendHttpAndGetJson<List<Repository>>(endpoint, HttpMethod.Get, patToken);
            if (!currentPageItems.Any()) yield break;

            foreach (var repo in currentPageItems) yield return repo;
        }
    }

    public async Task ForkRepo(string endPoint, string org, string repo, string patToken)
    {
        _logger.LogInformation("Forking repository on GitHub with org: {Org}, repo: {Repo}...", org, repo);

        var endpoint = $@"{endPoint}/repos/{org}/{repo}/forks";
        await _httpClient.SendHttp(endpoint, HttpMethod.Post, patToken);
    }

    public async Task<IEnumerable<PullRequest>> GetPullRequests(string endPoint, string org, string repo, string head,
        string patToken)
    {
        _logger.LogInformation("Getting pull requests on GitHub with org: {Org}, repo: {Repo}...", org, repo);

        var endpoint = $@"{endPoint}/repos/{org}/{repo}/pulls?head={head}";
        return await _httpClient.SendHttpAndGetJson<List<PullRequest>>(endpoint, HttpMethod.Get, patToken);
    }

    public async Task CreatePullRequest(string endPoint, string org, string repo, string head, string @base,
        string patToken)
    {
        _logger.LogInformation("Creating a new pull request on GitHub with org: {Org}, repo: {Repo}...", org, repo);

        var endpoint = $@"{endPoint}/repos/{org}/{repo}/pulls";
        await _httpClient.SendHttp(endpoint, HttpMethod.Post, patToken, new
        {
            title = "Auto dependencies upgrade by bot.",
            body = @"
Auto dependencies upgrade by bot. This is automatically generated by bot.

The bot tries to fetch all possible updates and modify the project files automatically.

This pull request may break or change the behavior of this application. Review with cautious!",
            head,
            @base
        });
    }

    public string GetPushPath(Server connectionConfiguration, Repository repo)
    {
        var pushPath = string.Format(connectionConfiguration.PushEndPoint,
                           $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}")
                       + $"/{connectionConfiguration.UserName}/{repo.Name}.git";
        return pushPath;
    }
}