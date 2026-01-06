namespace Aiursoft.NugetNinja.PrBot.Configuration;

/// <summary>
/// Configuration options for the NugetNinja PR Bot.
/// Provides clean separation of configuration concerns from business logic.
/// </summary>
public class PrBotOptions
{
    /// <summary>
    /// Whether to enable automatic localization after NugetNinja processing.
    /// </summary>
    public bool LocalizationEnabled { get; set; } = false;

    /// <summary>
    /// The Ollama API endpoint for localization (e.g., "https://api.deepseek.com/chat/completions").
    /// </summary>
    public string? OllamaApiEndpoint { get; set; }

    /// <summary>
    /// The Ollama model to use for localization (e.g., "deepseek-chat").
    /// </summary>
    public string? OllamaModel { get; set; }

    /// <summary>
    /// The API key for Ollama API.
    /// </summary>
    public string? OllamaApiKey { get; set; }

    /// <summary>
    /// Maximum concurrent requests for localization.
    /// </summary>
    public int LocalizationConcurrentRequests { get; set; } = 8;

    /// <summary>
    /// Target languages for localization (e.g., ["zh-CN", "en-US", "ja-JP"]).
    /// </summary>
    public string[] LocalizationTargetLanguages { get; set; } = [];
}
