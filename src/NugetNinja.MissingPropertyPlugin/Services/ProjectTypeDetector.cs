using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class ProjectTypeDetector
{
    private readonly ILogger<ProjectTypeDetector> _logger;

    public ProjectTypeDetector(ILogger<ProjectTypeDetector> logger)
    {
        _logger = logger;
    }
    
    public ProjectInfo Detect(Project project)
    {
        var useWeb = project.Sdk?.ToLower().EndsWith("web") ?? false;
        _logger.LogTrace("Project {Project} use web status is {UseWeb}", project, useWeb);
        
        var hasUtFeatures = project.ContainsTestLibrary() || project.IsTestProject == true.ToString();
        _logger.LogTrace("Project {Project} has UT features status is {HasUtFeatures}", project, hasUtFeatures);

        var packAsTool = project.PackAsTool == true.ToString();
        _logger.LogTrace("Project {Project} pack as tool status is {PackAsTool}", project, packAsTool);
        
        var hasVersion = !string.IsNullOrWhiteSpace(project.Version);
        _logger.LogTrace("Project {Project} built has version status is {HasVersion}", project, hasVersion);

        return new ProjectInfo
        {
            IsExecutable = packAsTool || (project.OutputType?.EndsWith("exe") ?? false),
            IsHttpServer = useWeb,
            IsUnitTest = hasUtFeatures,
            ShouldPackAsNugetTool = !hasUtFeatures && (hasVersion && packAsTool),
            ShouldPackAsNugetLibrary = !hasUtFeatures && (hasVersion || packAsTool)
        };
    }
}