using Aiursoft.Canon;
using Aiursoft.Dotlang.AspNetTranslate.Services;
using Aiursoft.NugetNinja.PrBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.PrBot.Services;

/// <summary>
/// Service for handling automatic localization of .NET projects.
/// Detects projects with Resources directories and generates localization files.
/// </summary>
public class LocalizationService
{
    private readonly TranslateEntry _translateEntry;
    private readonly RetryEngine _retryEngine;
    private readonly PrBotOptions _options;
    private readonly ILogger<LocalizationService> _logger;

    public LocalizationService(
        TranslateEntry translateEntry,
        RetryEngine retryEngine,
        IOptions<PrBotOptions> options,
        ILogger<LocalizationService> logger)
    {
        _translateEntry = translateEntry;
        _retryEngine = retryEngine;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Localizes all projects in the workspace that have Resources directories.
    /// Searches for all .csproj files and localizes each one with a Resources directory.
    /// </summary>
    /// <param name="workspacePath">Path to the workspace root directory</param>
    /// <returns>True if any localization was attempted, false otherwise</returns>
    public virtual async Task<bool> LocalizeProjectAsync(string workspacePath)
    {
        if (!_options.LocalizationEnabled)
        {
            _logger.LogWarning("Localization is disabled. Skipping localization step.");
            return false;
        }

        // Validate required configuration
        if (string.IsNullOrWhiteSpace(_options.OllamaApiEndpoint) ||
            string.IsNullOrWhiteSpace(_options.OllamaModel) ||
            string.IsNullOrWhiteSpace(_options.OllamaApiKey))
        {
            _logger.LogWarning("Localization is enabled but Ollama API configuration is incomplete. Skipping localization.");
            return false;
        }

        if (_options.LocalizationTargetLanguages.Length == 0)
        {
            _logger.LogWarning("No target languages configured for localization. Skipping localization.");
            return false;
        }

        // Find all .csproj files in the workspace
        var csprojFiles = Directory.GetFiles(workspacePath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                       !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();

        if (csprojFiles.Count == 0)
        {
            _logger.LogDebug("No .csproj files found in workspace: {WorkspacePath}", workspacePath);
            return false;
        }

        _logger.LogInformation("Found {Count} .csproj file(s) to check for localization", csprojFiles.Count);

        var localizedCount = 0;

        foreach (var csprojFile in csprojFiles)
        {
            var projectDir = Path.GetDirectoryName(csprojFile);
            if (string.IsNullOrEmpty(projectDir))
            {
                continue;
            }

            var resourcesDir = Path.Combine(projectDir, "Resources");
            if (!Directory.Exists(resourcesDir))
            {
                _logger.LogDebug("No Resources directory found for project: {CsprojFile}", Path.GetFileName(csprojFile));
                continue;
            }

            _logger.LogInformation("Resources directory detected for project: {CsprojFile}. Starting localization...",
                Path.GetFileName(csprojFile));

            try
            {
                await _retryEngine.RunWithRetry(async _ =>
                {
                    await LocalizeProjectDirectoryAsync(projectDir);
                }, attempts: 3);
                localizedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during localization of project: {CsprojFile} after 3 attempts", csprojFile);
                // Continue with other projects even if one fails
            }
        }

        if (localizedCount > 0)
        {
            _logger.LogInformation("Successfully localized {Count} project(s)", localizedCount);
            return true;
        }

        _logger.LogInformation("No projects with Resources directories found for localization");
        return false;
    }

    private async Task LocalizeProjectDirectoryAsync(string projectPath)
    {
        // Localize C# files
        _logger.LogInformation("Auto-generating view injections in: {ProjectPath}...", projectPath);
        await _translateEntry.AutoGenerateViewInjectionsAsync(
            projectPath,
            takeAction: true);

        // Localize C# files
        _logger.LogInformation("Localizing C# files in: {ProjectPath}...", projectPath);
        await _translateEntry.StartLocalizeContentInCSharpAsync(
            projectPath,
            _options.LocalizationTargetLanguages,
            takeAction: true,
            concurrentRequests: _options.LocalizationConcurrentRequests);

        // Localize DataAnnotations
        _logger.LogInformation("Localizing DataAnnotations in: {ProjectPath}...", projectPath);
        await _translateEntry.StartLocalizeDataAnnotationsAsync(
            projectPath,
            _options.LocalizationTargetLanguages,
            takeAction: true,
            concurrentRequests: _options.LocalizationConcurrentRequests);

        // Localize CSHTML files
        _logger.LogInformation("Localizing CSHTML files in: {ProjectPath}...", projectPath);
        await _translateEntry.StartLocalizeContentInCsHtmlAsync(
            projectPath,
            _options.LocalizationTargetLanguages,
            takeAction: true,
            concurentRequests: _options.LocalizationConcurrentRequests);

        _logger.LogInformation("Localization completed for project: {ProjectPath}", projectPath);
    }
}
