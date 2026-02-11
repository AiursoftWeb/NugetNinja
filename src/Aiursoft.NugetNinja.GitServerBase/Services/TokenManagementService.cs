using Microsoft.Extensions.Logging;
using Aiursoft.NugetNinja.GitServerBase.Models.Configuration;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;

namespace Aiursoft.NugetNinja.GitServerBase.Services;

public class TokenManagementService(
    IEnumerable<IVersionControlService> versionControls,
    TokenStoreService tokenStoreService,
    ILogger<TokenManagementService> logger)
{
    public async Task<string> GetCurrentTokenAsync(string providerName, string endPoint, string initialToken, bool isPrBot)
    {
        var service = versionControls.FirstOrDefault(v => v.GetName().Equals(providerName, StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException($"Provider {providerName} is not supported!");

        var gitLabTokens = await tokenStoreService.ReadTokensAsync() ?? new GitLabTokens();
        
        // 1. Determine current working token (prioritize disk, then initial config)
        var currentToken = isPrBot ? gitLabTokens.ContributeToken : gitLabTokens.MergeToken;
        currentToken = !string.IsNullOrEmpty(currentToken) ? currentToken : initialToken;

        if (string.IsNullOrEmpty(currentToken))
        {
            throw new InvalidOperationException($"Token for {providerName} at {endPoint} is not provided in config or disk.");
        }

        // 2. If the provider supports rotation, rotate it.
        if (service.SupportsTokenRotation)
        {
            logger.LogInformation("Provider {Provider} supports token rotation. Rotating...", providerName);
            var newToken = await service.RotateToken(endPoint, currentToken);
            
            // Update the record
            if (isPrBot) gitLabTokens.ContributeToken = newToken;
            else gitLabTokens.MergeToken = newToken;

            // 3. Persist immediately.
            await tokenStoreService.SaveTokensAsync(gitLabTokens);
            return newToken;
        }

        return currentToken;
    }
}
