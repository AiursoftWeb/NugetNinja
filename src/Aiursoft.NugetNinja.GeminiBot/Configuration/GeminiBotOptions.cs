namespace Aiursoft.NugetNinja.GeminiBot.Configuration;

/// <summary>
/// Configuration options for the Gemini Bot.
/// Provides clean separation of configuration concerns from business logic.
/// </summary>
public class GeminiBotOptions
{
    /// <summary>
    /// Workspace folder where repositories are cloned for processing.
    /// </summary>
    public string WorkspaceFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NugetNinjaWorkspace");

    /// <summary>
    /// Timeout for Gemini CLI execution.
    /// </summary>
    public TimeSpan GeminiTimeout { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Delay in milliseconds when waiting for a forked repository to become available.
    /// </summary>
    public int ForkWaitDelayMs { get; set; } = 5000;
}
