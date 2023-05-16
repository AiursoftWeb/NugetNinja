using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PrBot;

public class CommandRunner
{
    private readonly ILogger<CommandRunner> _logger;

    public CommandRunner(ILogger<CommandRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Run git command.
    /// </summary>
    /// <param name="path">Path</param>
    /// <param name="arguments">Arguments</param>
    /// <param name="integrateResultInProcess">integrateResultInProcess</param>
    /// <param name="timeout">timeout</param>
    /// <returns>Task</returns>
    public async Task<string> RunGit(string path, string arguments, bool integrateResultInProcess = true, TimeSpan? timeout = null)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        if (timeout == null)
        {
            timeout = TimeSpan.FromMinutes(2);
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WindowStyle = integrateResultInProcess ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Minimized,
                UseShellExecute = false,
                CreateNoWindow = integrateResultInProcess,
                RedirectStandardOutput = integrateResultInProcess,
                RedirectStandardError = integrateResultInProcess,
                WorkingDirectory = path
            }
        };

        _logger.LogTrace($"Running command: {path.TrimEnd('\\').Trim()} git {arguments}");

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            throw new GitCommandException(
                "Start Git failed! Please install Git at https://git-scm.com .",
                arguments,
                "Start git failed.",
                path);
        }

        var executeTask = Task.Run(process.WaitForExit);
        await Task.WhenAny(Task.Delay(timeout.Value), executeTask);
        if (!executeTask.IsCompleted)
        {
            throw new TimeoutException($@"Execute git command: git {arguments} at {path} was time out! Timeout is {timeout}.");
        }

        if (!integrateResultInProcess) return string.Empty;

        var consoleOutput = string.Empty;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        if (
            output.Contains("'git-lfs' was not found") ||
            error.Contains("'git-lfs' was not found") ||
            output.Contains("git-lfs: command not found") ||
            error.Contains("git-lfs: command not found"))
            throw new GitCommandException(
                "Start Git failed! Git LFS not found!",
                arguments,
                "Start git failed.",
                path);

        if (!string.IsNullOrWhiteSpace(error))
        {
            consoleOutput = error;
            if (error.Contains("fatal") || error.Contains("error:"))
            {
                _logger.LogTrace(consoleOutput);
                throw new GitCommandException(
                    $"Git command resulted an error: git {arguments} on {path} got result: {error}",
                    arguments,
                    error,
                    path);
            }
        }

        consoleOutput += output;
        _logger.LogTrace(consoleOutput);
        return consoleOutput;
    }
}