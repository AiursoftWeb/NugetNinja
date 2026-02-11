namespace Aiursoft.NugetNinja.GitServerBase.Models.Configuration;

public class MergeServer
{
    public string? Provider { get; set; }
    public string? EndPoint { get; set; }
    public string? UserName { get; set; }
    public string? Token { get; set; }
    public bool IsPrBot { get; set; } = false;

    public void Validate()
    {
        if (string.IsNullOrEmpty(Provider)) throw new InvalidOperationException("Server provider is not configured.");
        if (string.IsNullOrEmpty(EndPoint)) throw new InvalidOperationException($"Server endpoint is not configured for provider: {Provider}.");
        if (string.IsNullOrEmpty(UserName)) throw new InvalidOperationException($"Server username is not configured for provider: {Provider}.");
        if (string.IsNullOrEmpty(Token)) throw new InvalidOperationException($"Server token is not configured for provider: {Provider}.");
    }
}

