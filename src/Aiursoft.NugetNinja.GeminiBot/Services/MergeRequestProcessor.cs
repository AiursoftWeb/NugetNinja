using Aiursoft.CSTools.Services;
using Aiursoft.GitRunner;
using Aiursoft.GitRunner.Models;
using Aiursoft.NugetNinja.GeminiBot.Configuration;
using Aiursoft.NugetNinja.GeminiBot.Models;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.GeminiBot.Services;

/// <summary>
/// Handles checking and fixing failed merge requests.
/// Ensures bot's own PRs pass CI/CD before processing new issues.
/// </summary>
public class MergeRequestProcessor
{
    private readonly IVersionControlService _versionControl;
    private readonly WorkspaceManager _workspaceManager;
    private readonly CommandService _commandService;
    private readonly GeminiBotOptions _options;
    private readonly ILogger<MergeRequestProcessor> _logger;

    public MergeRequestProcessor(
        IVersionControlService versionControl,
        WorkspaceManager workspaceManager,
        CommandService commandService,
        IOptions<GeminiBotOptions> options,
        ILogger<MergeRequestProcessor> logger)
    {
        _versionControl = versionControl;
        _workspaceManager = workspaceManager;
        _commandService = commandService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Main method to process all open merge requests for the bot user.
    /// Checks pipeline status, downloads failure logs, and invokes Gemini to fix.
    /// </summary>
    public async Task<ProcessResult> ProcessMergeRequestsAsync(Server server)
    {
        try
        {
            _logger.LogInformation("Checking merge requests submitted by {UserName}...", server.UserName);

            var mergeRequests = await _versionControl.GetOpenMergeRequests(
                server.EndPoint,
                server.UserName,
                server.Token);

            var failedMRs = new List<(GitServerBase.Models.Abstractions.MergeRequestSearchResult mr, GitServerBase.Models.Abstractions.DetailedMergeRequest details)>();

            foreach (var mr in mergeRequests)
            {
                _logger.LogInformation("Checking MR #{IID}: {Title}...", mr.IID, mr.Title);

                var details = await _versionControl.GetMergeRequestDetails(
                    server.EndPoint,
                    server.UserName,
                    server.Token,
                    mr.ProjectId,
                    mr.IID);

                // Check if pipeline exists and has failed
                if (details.Pipeline != null && details.Pipeline.Status != "success")
                {
                    _logger.LogWarning("MR #{IID} has pipeline with status: {Status}", mr.IID, details.Pipeline.Status);

                    // Only process failed pipelines, skip running ones
                    if (details.Pipeline.Status == "failed")
                    {
                        failedMRs.Add((mr, details));
                    }
                }
                else
                {
                    _logger.LogInformation("MR #{IID} pipeline is {Status}, no action needed", mr.IID, details.Pipeline?.Status ?? "null");
                }
            }

            if (failedMRs.Count == 0)
            {
                _logger.LogInformation("No failed merge requests found. All clear!");
                return ProcessResult.Succeeded("No failed MRs to fix");
            }

            _logger.LogInformation("Found {Count} failed merge requests to fix", failedMRs.Count);

            // Process each failed MR
            foreach (var (mr, details) in failedMRs)
            {
                await CheckAndFixFailedPipelineAsync(mr, details, server);
            }

            return ProcessResult.Succeeded($"Processed {failedMRs.Count} failed MRs");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing merge requests");
            return ProcessResult.Failed("Error processing merge requests", ex);
        }
    }

    /// <summary>
    /// Check a single MR with failed pipeline, download logs, and invoke Gemini to fix.
    /// </summary>
    private async Task CheckAndFixFailedPipelineAsync(
        GitServerBase.Models.Abstractions.MergeRequestSearchResult mr,
        GitServerBase.Models.Abstractions.DetailedMergeRequest details,
        Server server)
    {
        try
        {
            _logger.LogInformation("Processing failed MR #{IID}: {Title}", mr.IID, mr.Title);

            if (details.Pipeline == null)
            {
                _logger.LogWarning("MR #{IID} has no pipeline information", mr.IID);
                return;
            }

            if (details.Pipeline.Id <= 0)
            {
                _logger.LogWarning("MR #{IID} has invalid pipeline ID: {PipelineId}", mr.IID, details.Pipeline.Id);
                return;
            }

            // CRITICAL: Pipeline runs in the SOURCE project (fork), not the target project!
            var pipelineProjectId = mr.SourceProjectId > 0 ? mr.SourceProjectId : mr.ProjectId;
            _logger.LogInformation("MR #{IID}: Using project ID {ProjectId} for pipeline operations (source: {SourceProjectId}, target: {TargetProjectId})",
                mr.IID, pipelineProjectId, mr.SourceProjectId, mr.ProjectId);

            // Get repository details from SOURCE project (where the branch exists)
            // The MR branch is in the fork, not in the target project!
            _logger.LogInformation("Getting repository details from source project {ProjectId}...", pipelineProjectId);
            var repository = await _versionControl.GetRepository(
                server.EndPoint,
                pipelineProjectId.ToString(),
                string.Empty,
                server.Token);

            // Get failure logs from SOURCE project (where pipeline runs)
            var failureLogs = await GetFailureLogsAsync(server, pipelineProjectId, details.Pipeline.Id);

            if (string.IsNullOrWhiteSpace(failureLogs))
            {
                _logger.LogWarning("No failure logs found for MR #{IID}", mr.IID);
                return;
            }

            // Clone the repository and checkout the MR branch
            var workPath = GetWorkspacePath(mr, repository);
            _logger.LogInformation("Cloning repository for MR #{IID} to {WorkPath}...", mr.IID, workPath);

            // Get the source branch from the MR
            var branchName = mr.SourceBranch ?? throw new InvalidOperationException($"MR #{mr.IID} has no source branch");

            await _workspaceManager.ResetRepo(
                workPath,
                branchName, // Checkout the MR's source branch
                repository.CloneUrl ?? throw new InvalidOperationException($"Repository clone URL is null for MR {mr.IID}"),
                CloneMode.Full,
                $"{server.UserName}:{server.Token}");

            // Build prompt with failure context
            var prompt = BuildFailurePrompt(mr, details, failureLogs);
            _logger.LogInformation("Invoking Gemini CLI to fix MR #{IID}...", mr.IID);

            var geminiSuccess = await InvokeGeminiCliAsync(workPath, prompt);
            if (!geminiSuccess)
            {
                _logger.LogError("Gemini CLI failed to process MR #{IID}", mr.IID);
                return;
            }

            // Wait for Gemini to finish
            await Task.Delay(1000);

            // Check for changes
            if (!await _workspaceManager.PendingCommit(workPath))
            {
                _logger.LogInformation("MR #{IID} - Gemini made no changes", mr.IID);
                return;
            }

            // Commit and push fixes
            _logger.LogInformation("MR #{IID} has pending changes. Committing and pushing...", mr.IID);
            await _workspaceManager.SetUserConfig(workPath, server.DisplayName, server.UserEmail);

            var commitMessage = $"Fix pipeline failure for MR #{mr.IID}\n\nAutomatically generated fix by Gemini Bot.";

            var saved = await _workspaceManager.CommitToBranch(workPath, commitMessage, branchName);
            if (!saved)
            {
                _logger.LogError("Failed to commit changes for MR #{IID}", mr.IID);
                return;
            }

            // Push to the MR's source branch
            var pushPath = _versionControl.GetPushPath(server, repository);
            await _workspaceManager.Push(workPath, branchName, pushPath, force: true);

            _logger.LogInformation("Successfully fixed and pushed changes for MR #{IID}", mr.IID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing MR #{IID}", mr.IID);
        }
    }

    /// <summary>
    /// Download failure logs from all failed jobs in the pipeline.
    /// </summary>
    private async Task<string> GetFailureLogsAsync(Server server, int projectId, int pipelineId)
    {
        try
        {
            _logger.LogInformation("Fetching jobs for pipeline {PipelineId} in project {ProjectId}...", pipelineId, projectId);

            var jobs = await _versionControl.GetPipelineJobs(
                server.EndPoint,
                server.Token,
                projectId,
                pipelineId);

            if (jobs.Count == 0)
            {
                _logger.LogWarning("Pipeline {PipelineId} has no jobs (may have been deleted or not started yet)", pipelineId);
                return string.Empty;
            }

            var failedJobs = jobs.Where(j => j.Status == "failed").ToList();

            if (failedJobs.Count == 0)
            {
                _logger.LogWarning("Pipeline {PipelineId} has no failed jobs", pipelineId);
                return string.Empty;
            }

            _logger.LogInformation("Found {Count} failed jobs in pipeline {PipelineId}", failedJobs.Count, pipelineId);

            var allLogs = new System.Text.StringBuilder();

            foreach (var job in failedJobs)
            {
                _logger.LogInformation("Downloading log for failed job: {JobName} (ID: {JobId})", job.Name, job.Id);

                var log = await _versionControl.GetJobLog(
                    server.EndPoint,
                    server.Token,
                    projectId,
                    job.Id);

                allLogs.AppendLine($"\n\n=== Job: {job.Name} (Stage: {job.Stage}) ===");
                allLogs.AppendLine(log);
                allLogs.AppendLine("=== End of Job Log ===\n");
            }

            return allLogs.ToString();
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _logger.LogWarning("Pipeline {PipelineId} not found (404) - it may have been deleted or never existed. Skipping...", pipelineId);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting failure logs for pipeline {PipelineId}", pipelineId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Build the prompt for Gemini with MR context and failure logs.
    /// </summary>
    private string BuildFailurePrompt(
        GitServerBase.Models.Abstractions.MergeRequestSearchResult mr,
        GitServerBase.Models.Abstractions.DetailedMergeRequest details,
        string failureLogs)
    {
        return $@"Merge Request #{mr.IID}: {mr.Title}

Pipeline Web URL: {details.Pipeline?.WebUrl}
Pipeline Status: {details.Pipeline?.Status}

The CI/CD pipeline for this merge request has FAILED. Your task is to analyze the failure logs below and fix the code to make the pipeline pass.

=== FAILURE LOGS ===
{failureLogs}
=== END OF FAILURE LOGS ===

Please analyze the failure logs, identify the root cause, and make the necessary code changes to fix the build/test failures.";
    }

    /// <summary>
    /// Invoke Gemini CLI to fix the code. Similar to IssueProcessor but for MR fixing.
    /// </summary>
    private async Task<bool> InvokeGeminiCliAsync(string workPath, string taskDescription)
    {
        string? tempFile = null;
        var gitPath = Path.Combine(workPath, ".git");
        var gitBackupPath = workPath + "-hidden-git";

        try
        {
            // Write task to temp file
            tempFile = Path.Combine(workPath, ".gemini-task.txt");
            await File.WriteAllTextAsync(tempFile, taskDescription);

            // Hide .git directory to prevent Gemini from manipulating git
            if (Directory.Exists(gitPath))
            {
                _logger.LogInformation("Hiding .git directory to prevent Gemini CLI from manipulating git...");
                Directory.Move(gitPath, gitBackupPath);
            }

            _logger.LogInformation("Running Gemini CLI in {WorkPath}", workPath);

            var geminiCommand = "gemini --yolo < .gemini-task.txt";
            var (code, output, error) = await _commandService.RunCommandAsync(
                bin: "/bin/bash",
                arg: $"-c \"{geminiCommand}\"",
                path: workPath,
                timeout: _options.GeminiTimeout);

            if (code != 0)
            {
                _logger.LogError("Gemini CLI failed with exit code {Code}. Output: {Output}. Error: {Error}", code, output, error);
                return false;
            }

            _logger.LogInformation("Gemini CLI completed successfully. It says: {Output}", output);
            return true;
        }
        finally
        {
            // Restore .git directory
            if (Directory.Exists(gitBackupPath))
            {
                try
                {
                    _logger.LogInformation("Restoring .git directory...");
                    if (Directory.Exists(gitPath))
                    {
                        Directory.Delete(gitPath, recursive: true);
                    }
                    Directory.Move(gitBackupPath, gitPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore .git directory from backup!");
                }
            }

            // Clean up temp file
            if (tempFile != null && File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file: {FilePath}", tempFile);
                }
            }
        }
    }

    private string GetWorkspacePath(GitServerBase.Models.Abstractions.MergeRequestSearchResult mr, Repository repository)
    {
        var repoName = repository.Name ?? "unknown";
        return Path.Combine(_options.WorkspaceFolder, $"{mr.ProjectId}-{repoName}-mr-{mr.IID}");
    }
}
