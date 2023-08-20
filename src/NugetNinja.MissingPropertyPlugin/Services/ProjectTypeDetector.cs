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
    
    public ProjectType Detect(Project project)
    {

        var sdk = project.Sdk;
        var output = project.OutputType;

        if (project.IsTest())
        {
            return ProjectType.UnitTest;
        }
        
        if (project.PackageLicenseFile)
    }
}