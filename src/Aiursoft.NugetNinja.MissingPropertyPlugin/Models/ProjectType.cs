namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class ProjectInfo
{
    public bool IsUnitTest { get; set; }
    public bool IsWindowsExecutable { get; set; }
    public bool IsExecutable { get; set; }
    public bool IsHttpServer { get; set; }
    
    public bool ShouldPackAsNugetTool { get; set; }
    public bool ShouldPackAsNugetLibrary { get; set; }
}