using Microsoft.Extensions.Logging;
using System.Text.Json;
using Aiursoft.NugetNinja.GitServerBase.Models.Configuration;

namespace Aiursoft.NugetNinja.GitServerBase.Services;

public class TokenStoreService
{
    private readonly ILogger<TokenStoreService> _logger;
    private const string TokenFileName = "gitlab_tokens.ini";
    private readonly string _workspacePath; 

    public TokenStoreService(ILogger<TokenStoreService> logger, string workspacePath)
    {
        _logger = logger;
        _workspacePath = workspacePath;
        
        // Ensure the directory exists
        Directory.CreateDirectory(_workspacePath);
    }

    private string GetTokenFilePath()
    {
        return Path.Combine(_workspacePath, TokenFileName);
    }

    private string GetTempTokenFilePath()
    {
        return Path.Combine(_workspacePath, TokenFileName + ".tmp");
    }

    public async Task<GitLabTokens?> ReadTokensAsync()
    {
        var filePath = GetTokenFilePath();
        if (!File.Exists(filePath))
        {
            _logger.LogInformation("GitLab tokens file not found at {FilePath}.", filePath);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var tokens = await JsonSerializer.DeserializeAsync<GitLabTokens>(stream);
            _logger.LogInformation("Successfully read GitLab tokens from {FilePath}.", filePath);
            return tokens;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read GitLab tokens from {FilePath}. Falling back to environment variables.", filePath);
            return null;
        }
    }

    public async Task SaveTokensAsync(GitLabTokens tokens)
    {
        var filePath = GetTokenFilePath();
        var tempFilePath = GetTempTokenFilePath();

        try
        {
            // Log the new tokens (double-safety)
            _logger.LogInformation("Saving new GitLab tokens. Contribute token (partial): {ContributeTokenPartial}, Merge token (partial): {MergeTokenPartial}",
                tokens.ContributeToken?.Substring(0, Math.Min(tokens.ContributeToken.Length, 10)),
                tokens.MergeToken?.Substring(0, Math.Min(tokens.MergeToken.Length, 10)));

            await using (var stream = File.Create(tempFilePath))
            {
                await JsonSerializer.SerializeAsync(stream, tokens, new JsonSerializerOptions { WriteIndented = true });
            }

            // Atomic move
            File.Move(tempFilePath, filePath, overwrite: true);
            _logger.LogInformation("Successfully saved GitLab tokens to {FilePath}.", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save GitLab tokens to {FilePath}.", filePath);
            throw; // Re-throw to crash the application as per requirement
        }
    }
}
