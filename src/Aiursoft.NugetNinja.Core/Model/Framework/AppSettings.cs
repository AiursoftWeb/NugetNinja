namespace Aiursoft.NugetNinja.Core.Model.Framework;

public class AppSettings
{
    public bool AllowCross { get; set; }
    public bool Verbose { get; set; }
    public bool AllowPreview { get; set; }
    public string CustomNugetServer { get; set; } = string.Empty;
    public string PatToken { get; set; } = string.Empty;
}
