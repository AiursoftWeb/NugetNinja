namespace Aiursoft.NugetNinja.PrBot;

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
}