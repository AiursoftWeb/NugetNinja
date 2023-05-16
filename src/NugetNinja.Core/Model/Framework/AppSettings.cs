namespace Aiursoft.NugetNinja.Core;

public class AppSettings
{
    public bool AllowCross { get; set; }
    public bool Verbose { get; set; }
    public bool AllowPreview { get; set; }
    public string CustomNugetServer { get; set; }
    public string PatToken { get; set; }
}
