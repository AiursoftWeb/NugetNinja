﻿using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PrBot;

/// <summary>
///     Workspace initializer.
/// </summary>
public class WorkspaceManager
{
    private readonly CommandRunner _commandRunner;
    private readonly ILogger<WorkspaceManager> _logger;
    private readonly RetryEngine _retryEngine;

    public WorkspaceManager(
        ILogger<WorkspaceManager> logger,
        RetryEngine retryEngine,
        CommandRunner commandRunner)
    {
        _logger = logger;
        _retryEngine = retryEngine;
        _commandRunner = commandRunner;
    }

    /// <summary>
    ///     Get current branch from a git repo.
    /// </summary>
    /// <param name="path">Path</param>
    /// <returns>Current branch.</returns>
    private async Task<string> GetBranch(string path)
    {
        var gitBranchOutput = await _commandRunner.RunGit(path, "rev-parse --abbrev-ref HEAD");
        return gitBranchOutput
            .Split('\n')
            .Single(s => !string.IsNullOrWhiteSpace(s))
            .Trim();
    }

    private async Task SwitchToBranch(string sourcePath, string targetBranch, bool fromCurrent)
    {
        var currentBranch = await GetBranch(sourcePath);
        if (string.Equals(currentBranch, targetBranch, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            await _commandRunner.RunGit(sourcePath, $"checkout -b {targetBranch}");
        }
        catch (GitCommandException e) when (e.Message.Contains("already exists"))
        {
            if (fromCurrent)
            {
                await _commandRunner.RunGit(sourcePath, $"branch -D {targetBranch}");
                await SwitchToBranch(sourcePath, targetBranch, fromCurrent);
            }
            else
            {
                await _commandRunner.RunGit(sourcePath, $"checkout {targetBranch}");
            }
        }
    }

    /// <summary>
    ///     Get remote origin's URL from a local git repo.
    /// </summary>
    /// <param name="path">Path.</param>
    /// <returns>Remote URL.</returns>
    private async Task<string> GetRemoteUrl(string path)
    {
        var gitRemoteOutput = await _commandRunner.RunGit(path, "remote -v");
        return gitRemoteOutput
            .Split('\n')
            .First(t => t.StartsWith("origin"))
            .Substring(6)
            .Replace("(fetch)", string.Empty)
            .Replace("(push)", string.Empty)
            .Trim();
    }

    /// <summary>
    ///     Clone a repo.
    /// </summary>
    /// <param name="path">Path on disk.</param>
    /// <param name="branch">Init branch.</param>
    /// <param name="endPoint">Endpoint. Used for Git clone.</param>
    /// <returns>Task</returns>
    private async Task Clone(string path, string branch, string endPoint)
    {
        await _commandRunner.RunGit(path, $"clone -b {branch} {endPoint} .");
    }

    /// <summary>
    ///     Switch a folder to a target branch (Latest remote).
    ///     Supports empty folder. We will clone the repo there.
    ///     Supports folder with existing content. We will clean that folder and checkout to the target branch.
    /// </summary>
    /// <param name="path">Path</param>
    /// <param name="branch">Branch name</param>
    /// <param name="endPoint">Git clone endpoint.</param>
    /// <returns>Task</returns>
    public async Task ResetRepo(string path, string branch, string endPoint)
    {
        try
        {
            var remote = await GetRemoteUrl(path);
            if (!string.Equals(remote, endPoint, StringComparison.OrdinalIgnoreCase))
                throw new GitCommandException(
                    $"The repository with remote: '{remote}' is not a repository for {endPoint}.", "remote -v", remote,
                    path);

            await _commandRunner.RunGit(path, "reset --hard HEAD");
            await _commandRunner.RunGit(path, "clean . -fdx");
            await SwitchToBranch(path, branch, false);
            await Fetch(path);
            await _commandRunner.RunGit(path, $"reset --hard origin/{branch}");
        }
        catch (GitCommandException e) when (
            e.Message.Contains("not a git repository") ||
            e.Message.Contains("unknown revision or path") ||
            e.Message.Contains($"is not a repository for {endPoint}"))
        {
            ClearPath(path);
            await Clone(path, branch, endPoint);
        }
    }

    /// <summary>
    ///     Do a commit. (With adding local changes)
    /// </summary>
    /// <param name="sourcePath">Commit path.</param>
    /// <param name="message">Commie message.</param>
    /// <param name="branch">Branch</param>
    /// <returns>Saved.</returns>
    public async Task<bool> CommitToBranch(string sourcePath, string message, string branch)
    {
        await _commandRunner.RunGit(sourcePath, "add .");
        await SwitchToBranch(sourcePath, branch, true);
        var commitResult = await _commandRunner.RunGit(sourcePath, $@"commit -m ""{message}""");
        return !commitResult.Contains("nothing to commit, working tree clean");
    }

    public async Task SetUserConfig(string sourcePath, string username, string email)
    {
        await _commandRunner.RunGit(sourcePath, $@"config user.name ""{username}""");
        await _commandRunner.RunGit(sourcePath, $@"config user.email ""{email}""");
    }

    /// <summary>
    ///     Push a local folder to remote.
    /// </summary>
    /// <param name="sourcePath">Folder path..</param>
    /// <param name="branch">Remote branch.</param>
    /// <param name="endpoint">Endpoint</param>
    /// <param name="force">Force</param>
    /// <returns>Pushed.</returns>
    public async Task Push(string sourcePath, string branch, string endpoint, bool force = false)
    {
        // Set origin url.
        try
        {
            await _commandRunner.RunGit(sourcePath, $@"remote set-url ninja {endpoint}");
        }
        catch (GitCommandException e) when (e.GitOutput.Contains("No such remote"))
        {
            await _commandRunner.RunGit(sourcePath, $@"remote add ninja {endpoint}");
        }

        // Push to that origin.
        try
        {
            var forceString = force ? "--force" : string.Empty;

            var command = $@"push --set-upstream ninja {branch} {forceString}";
            _logger.LogInformation($"Running git {command}");
            await _commandRunner.RunGit(sourcePath, command);
        }
        catch (GitCommandException e) when (e.GitOutput.Contains("rejected]"))
        {
            // In this case, the remote branch is later than local.
            // So we might have some conflict.
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Git push failed to {sourcePath}, branch {branch}, endpoint {endpoint}", ex);
            throw;
        }
    }

    /// <summary>
    ///     If current path is pending a git commit.
    /// </summary>
    /// <param name="sourcePath">Path</param>
    /// <returns>Bool</returns>
    public async Task<bool> PendingCommit(string sourcePath)
    {
        var statusResult = await _commandRunner.RunGit(sourcePath, @"status");
        var clean = statusResult.Contains("working tree clean");
        return !clean;
    }

    private Task Fetch(string path)
    {
        return _retryEngine.RunWithTry(
            async attempt =>
            {
                var workJob = _commandRunner.RunGit(path, "fetch --verbose",
                    attempt % 2 == 0);
                var waitJob = Task.Delay(TimeSpan.FromSeconds(attempt * 50));
                await Task.WhenAny(workJob, waitJob);
                if (workJob.IsCompleted)
                    return await workJob;
                throw new TimeoutException("Git fetch job has exceeded the timeout and we have to retry it.");
            });
    }

    private void ClearPath(string path)
    {
        var di = new DirectoryInfo(path);
        foreach (var file in di.GetFiles()) file.Delete();

        foreach (var dir in di.GetDirectories()) dir.Delete(true);
    }
}