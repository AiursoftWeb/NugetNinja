using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Utils;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.Core.Model.Workspace;

public class Model
{
    public List<Project> AllProjects { get; set; } = [];

    public List<Package> AllPackages { get; set; } = [];
    
    public NinjaConfig NinjaConfig { get; set; } = new();
    
    public string RootPath { get; set; } = null!;

    public async Task<Project> IncludeProject(string path, ILogger logger)
    {
        var projectInDatabases = AllProjects.FirstOrDefault(p => p.PathOnDisk == path);
        if (projectInDatabases != null)
        {
            return projectInDatabases;
        }

        logger.LogTrace("Inserting new project: {Path}", path);
        var builtProject = await BuildNewProject(path, logger);
        AllProjects.Add(builtProject);
        return builtProject;
    }

    private async Task<Project> BuildNewProject(string csprojPath, ILogger logger)
    {
        var csprojFolder = new FileInfo(csprojPath).Directory?.FullName
                           ?? throw new IOException(
                               $"Can not get the .csproj file location based on path: '{csprojPath}'!");
        var csprojContent = await File.ReadAllTextAsync(csprojPath);
        var csprojDoc = new HtmlDocument();
        csprojDoc.LoadHtml(csprojContent);
        var packageReferences = GetPackageReferences(csprojDoc);
        var projectReferences = GetProjectReferences(csprojDoc, csprojFolder);
        var frameworkReferences = GetFrameworkReferences(csprojDoc);

        var subProjectReferenceObjects = new List<Project>();
        foreach (var projectReference in projectReferences)
        {
            var projectObject = await IncludeProject(projectReference, logger);
            subProjectReferenceObjects.Add(projectObject);
        }

        logger.LogTrace("Parsing new project: {Path}", csprojPath);
        var project = new Project(csprojPath, csprojDoc.DocumentNode)
        {
            PackageReferences = packageReferences.ToList(),
            ProjectReferences = subProjectReferenceObjects,
            FrameworkReferences = frameworkReferences.ToList()
        };
        return project;
    }

    private Package[] GetPackageReferences(HtmlDocument doc)
    {
        var packageReferences = doc.DocumentNode
            .Descendants("PackageReference")
            .Select(p => new Package(
                p.Attributes["Include"]?.Value ?? p.Attributes["Update"]?.Value ?? "Unknown",
                p.Attributes["Version"]?.Value ?? "0.0.1"))
            .ToArray();

        foreach (var package in packageReferences)
            if (!AllPackages.Any(p =>
                    p.Name == package.Name &&
                    p.Version == package.Version))
                AllPackages.Add(package);

        return packageReferences;
    }

    private string[] GetProjectReferences(HtmlDocument doc, string csprojFolder)
    {
        var projectReferences = doc.DocumentNode
            .Descendants("ProjectReference")
            .Select(p => p.Attributes["Include"].Value)
            .Select(p => StringExtensions.GetAbsolutePath(csprojFolder, p))
            .ToArray();

        return projectReferences;
    }

    private string[] GetFrameworkReferences(HtmlDocument doc)
    {
        var projectReferences = doc.DocumentNode
            .Descendants("FrameworkReference")
            .Select(p => p.Attributes["Include"].Value)
            .ToArray();

        return projectReferences;
    }
}