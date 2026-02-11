namespace Aiursoft.NugetNinja.GitServerBase.Models;

public class Server
{
    public string Provider { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public string PushEndPoint { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ContributionBranch { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    public bool OnlyUpdate { get; set; } = false;
    public bool IsPrBot { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrEmpty(Provider)) throw new InvalidOperationException("Server provider is not configured.");
        if (string.IsNullOrEmpty(EndPoint)) throw new InvalidOperationException($"Server endpoint is not configured for provider: {Provider}.");
        if (string.IsNullOrEmpty(UserName)) throw new InvalidOperationException($"Server username is not configured for provider: {Provider}.");
        if (string.IsNullOrEmpty(Token)) throw new InvalidOperationException($"Server token is not configured for provider: {Provider}.");
    }
}
