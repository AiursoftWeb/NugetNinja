using Aiursoft.CSTools.Tools;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.MissingPropertyPlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Services;

public class ProjectTypeDetector
{
    private readonly ILogger<ProjectTypeDetector> _logger;

    public ProjectTypeDetector(ILogger<ProjectTypeDetector> logger)
    {
        _logger = logger;
    }
    
    public ProjectInfo Detect(Project project)
    {
        var hasProgram = CheckForProgramCs(project.PathOnDisk);
        
        var isWebProject = project.Sdk?.ToLower().EndsWith("web") ?? false;
        _logger.LogTrace("Project {Project} is web project status is {UseWeb}", project, isWebProject);

        var hasWindows = project.UseWindowsForms.IsTrue() || project.UseWPF.IsTrue(); 
        _logger.LogTrace("Project {Project} has Windows features status is {HasWindows}", project, hasWindows);

        var hasUtFeatures = project.ContainsTestLibrary() || project.IsTestProject.IsTrue();
        _logger.LogTrace("Project {Project} has UT features status is {HasUtFeatures}", project, hasUtFeatures);

        var packAsTool = project.PackAsTool.IsTrue();
        _logger.LogTrace("Project {Project} pack as tool status is {PackAsTool}", project, packAsTool);
        
        var hasVersion = !string.IsNullOrWhiteSpace(project.Version);
        _logger.LogTrace("Project {Project} built has version status is {HasVersion}", project, hasVersion);

        var isExecutable = hasProgram || packAsTool || isWebProject || (project.OutputType?.ToLower().EndsWith("exe") ?? false);
        return new ProjectInfo
        {
            IsExecutable = isExecutable,
            IsWindowsExecutable = hasWindows && isExecutable,
            IsHttpServer = isWebProject,
            IsUnitTest = hasUtFeatures,
            ShouldPackAsNugetTool = !hasUtFeatures && (hasVersion && packAsTool),
            ShouldPackAsNugetLibrary = !hasUtFeatures && (hasVersion || packAsTool)
        };
    }
    
    private bool CheckForProgramCs(string filePath)
    {
        var directoryPath = Path.GetDirectoryName(filePath);
        if (directoryPath == null) return false;
        var files = Directory.GetFiles(directoryPath, "Program.cs");
        return files.Length > 0;

    }
}