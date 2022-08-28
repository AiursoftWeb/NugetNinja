

namespace Aiursoft.NugetNinja.PrBot;

/// <summary>
/// A git command exception.
/// </summary>
public class GitCommandException : Exception
{
    /// <summary>
    /// Creates new GitCommandException
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="command">Command tried to run</param>
    /// <param name="result">Result.</param>
    /// <param name="path">Path.</param>
    public GitCommandException(
        string message,
        string command,
        string result,
        string path)
        : base(message)
    {
        Command = command;
        GitOutput = result;
        Path = path;
    }

    /// <summary>
    /// Command tried to run.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Output.
    /// </summary>
    public string GitOutput { get; }

    /// <summary>
    /// Executing path.
    /// </summary>
    public string Path { get; }
}
