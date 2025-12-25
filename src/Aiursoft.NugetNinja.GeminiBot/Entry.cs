using Aiursoft.CSTools.Services;
using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Models;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers.GitLab;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.GeminiBot;

public class Entry(
    CommandService commandService,
    IOptions<List<Server>> servers,
    IEnumerable<IVersionControlService> versionControls,
    WorkspaceManager workspaceManager,
    IHttpClientFactory httpClientFactory,
    ILogger<Entry> logger)
{
    private readonly List<Server> _servers = servers.Value;

    private readonly string _workspaceFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NugetNinjaWorkspace");

    public async Task RunAsync()
    {
        logger.LogInformation("Starting Gemini Bot for issue processing...");

        foreach (var server in _servers)
        {
            logger.LogInformation("Processing server: {ServerProvider}...", server.Provider);
            var serviceProvider = versionControls.First(v => v.GetName() == server.Provider);
            await RunServerAsync(server, serviceProvider);
        }
    }

    private async Task RunServerAsync(Server server, IVersionControlService versionControl)
    {
        var assignedIssues = await versionControl
            .GetAssignedIssues(server.EndPoint, server.UserName, server.Token)
            .ToListAsync();

        logger.LogInformation("Got {IssuesCount} issues assigned to {UserName}", assignedIssues.Count, server.UserName);
        logger.LogInformation("\r\n\r\n");
        logger.LogInformation("================================================================");
        logger.LogInformation("\r\n\r\n");

        foreach (var issue in assignedIssues)
            try
            {
                logger.LogInformation("Processing issue: {Issue}...", issue);
                await ProcessIssue(issue, server, versionControl);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Crashed when processing issue: {Issue}!", issue);
            }
            finally
            {
                logger.LogInformation("\r\n\r\n");
                logger.LogInformation("================================================================");
                logger.LogInformation("\r\n\r\n");
            }
    }

    private async Task ProcessIssue(Issue issue, Server connectionConfiguration, IVersionControlService versionControl)
    {
        if (string.IsNullOrWhiteSpace(issue.Title))
            throw new InvalidDataException($"Issue {issue.Id} has no title!");

        // Check if this is a GitLab service (we need it for HasOpenMergeRequest)
        var gitlabService = versionControl as GitLabService;
        if (gitlabService != null)
        {
            // Check if issue already has an open MR
            var hasOpenMr = await gitlabService.HasOpenMergeRequest(
                connectionConfiguration.EndPoint,
                issue.ProjectId,
                issue.Iid,
                connectionConfiguration.Token);

            if (hasOpenMr)
            {
                logger.LogInformation("Issue #{IssueId} already has an open MR. Skipping...", issue.Iid);
                return;
            }
        }

        // Get project details to get clone URL
        logger.LogInformation("Fetching project details for issue #{IssueId}...", issue.Iid);
        var endpoint = $"{connectionConfiguration.EndPoint}/api/v4/projects/{issue.ProjectId}";
        var httpClient = httpClientFactory.CreateClient();
        var httpWrapper = new HttpWrapper(Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpWrapper>.Instance, httpClient);
        var project = await httpWrapper.SendHttpAndGetJson<GitLabProject>(endpoint, HttpMethod.Get, connectionConfiguration.Token);

        if (project?.HttpUrlToRepo == null || project.DefaultBranch == null || project.PathWithNameSpace == null)
            throw new InvalidDataException($"Could not get project details for issue {issue.Id}");

        // Clone locally.
        var workPath = Path.Combine(_workspaceFolder, $"{issue.ProjectId}-{project.Path}-issue-{issue.Iid}");
        logger.LogInformation("Cloning repository for issue #{IssueId} to {WorkPath}...", issue.Iid, workPath);
        await workspaceManager.ResetRepo(
            workPath,
            project.DefaultBranch,
            project.HttpUrlToRepo,
            CloneMode.Full,
            $"{connectionConfiguration.UserName}:{connectionConfiguration.Token}");

        // Build task description for Gemini
        var taskDescription = $"Issue #{issue.Iid}: {issue.Title}\n\n{issue.Description ?? "No description provided."} \n\nPlease analyze this issue and make the necessary code changes to resolve it.";

        // Run Gemini CLI with --yolo flag
        logger.LogInformation("Invoking Gemini CLI to process issue #{IssueId}...", issue.Iid);
        var geminiSuccess = await InvokeGeminiCli(workPath, taskDescription);

        if (!geminiSuccess)
        {
            logger.LogWarning("Gemini CLI failed to process issue #{IssueId}", issue.Iid);
            return;
        }

        //Consider changes...
        if (!await workspaceManager.PendingCommit(workPath))
        {
            logger.LogInformation("Issue #{IssueId} - Gemini made no changes. Skipping...", issue.Iid);
            return;
        }

        logger.LogInformation("Issue #{IssueId} has pending changes. Creating MR...", issue.Iid);
        await workspaceManager.SetUserConfig(workPath, connectionConfiguration.DisplayName,
            connectionConfiguration.UserEmail);

        var commitMessage = $"Fix for issue #{issue.Iid}: {issue.Title}\n\nAutomatically generated by Gemini Bot.";
        var branchName = $"fix-issue-{issue.Iid}";
        var saved = await workspaceManager.CommitToBranch(workPath, commitMessage, branchName);
        if (!saved)
        {
            logger.LogInformation("Issue #{IssueId} - Failed to commit changes. Skipping...", issue.Iid);
            return;
        }

        // Get organization from project namespace
        var orgName = project.Namespace?.FullPath ?? throw new InvalidDataException($"Project namespace is null for issue {issue.Id}");
        var repoName = project.Path ?? throw new InvalidDataException($"Project path is null for issue {issue.Id}");

        // Fork repo if needed.
        if (!await versionControl.RepoExists(
                connectionConfiguration.EndPoint,
                connectionConfiguration.UserName,
                repoName,
                connectionConfiguration.Token))
        {
            await versionControl.ForkRepo(
                connectionConfiguration.EndPoint,
                orgName,
                repoName,
                connectionConfiguration.Token);
            await Task.Delay(5000);
            while (!await versionControl.RepoExists(
                       connectionConfiguration.EndPoint,
                       connectionConfiguration.UserName,
                       repoName,
                       connectionConfiguration.Token))
                await Task.Delay(5000);
        }

        // Create a repo object for push path generation
        var repo = new Repository
        {
            Id = issue.ProjectId,
            Name = repoName,
            FullName = project.PathWithNameSpace,
            Owner = new User { Login = orgName },
            DefaultBranch = project.DefaultBranch,
            CloneUrl = project.HttpUrlToRepo
        };

        // Push to forked repo.
        var pushPath = versionControl.GetPushPath(connectionConfiguration, repo);
        await workspaceManager.Push(
            workPath,
            branchName,
            pushPath,
            true);

        var existingPullRequestsByBot = (await versionControl.GetPullRequests(
                connectionConfiguration.EndPoint,
                orgName,
                repoName,
                $"{connectionConfiguration.UserName}:{branchName}",
                connectionConfiguration.Token))
            .Where(p => string.Equals(p.User?.Login, connectionConfiguration.UserName,
                StringComparison.OrdinalIgnoreCase));

        if (existingPullRequestsByBot.All(p => p.State != "open"))
            // Create a new pull request.
            await versionControl.CreatePullRequest(
                connectionConfiguration.EndPoint,
                orgName,
                repoName,
                $"{connectionConfiguration.UserName}:{branchName}",
                project.DefaultBranch,
                $"Fix for issue #{issue.Iid}: {issue.Title}",
                $@"
Automatically generated by Gemini Bot to fix issue #{issue.Iid}.

## Issue
{issue.Title}

{issue.Description ?? "No description provided."}

## Changes
This pull request contains automated fixes generated by the Gemini Bot.

Please review carefully before merging.",
                connectionConfiguration.Token);
        else
            logger.LogInformation("Skipped creating new pull request for issue #{IssueId} because one already exists", issue.Iid);
    }

    private async Task<bool> InvokeGeminiCli(string workPath, string taskDescription)
    {
        try
        {
            // Use a safer approach: write task to a temp file and pass filename to gemini
            var tempFile = Path.Combine(workPath, ".gemini-task.txt");
            await File.WriteAllTextAsync(tempFile, taskDescription);

            logger.LogInformation("Running: gemini --yolo with task from file in {WorkPath}", workPath);

            // Use stdin redirection instead of pipe to avoid quoting issues
            var geminiCommand = "gemini --yolo < .gemini-task.txt";
            var (code, output, error) = await commandService.RunCommandAsync(
                bin: "/bin/bash",
                arg: $"-c \"{geminiCommand}\"",
                path: workPath,
                timeout: TimeSpan.FromMinutes(20));

            // Clean up temp file
            try { File.Delete(tempFile); } catch { /* ignore cleanup errors */ }

            if (code != 0)
            {
                logger.LogError("Gemini CLI failed with exit code {Code}. Output: {Output}. Error: {Error}", code, output, error);
                return false;
            }

            logger.LogInformation("Gemini CLI completed successfully. Gemini said: {Output}", output);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception while invoking Gemini CLI");
            return false;
        }
    }
}
