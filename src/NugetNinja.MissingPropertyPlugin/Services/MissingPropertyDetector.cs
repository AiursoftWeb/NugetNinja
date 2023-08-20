using Aiursoft.CSTools.Tools;
using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyDetector : IActionDetector
{
    private readonly bool _enforceNullable = false;
    private readonly ProjectTypeDetector _projectTypeDetector;
    private readonly ILogger<MissingPropertyDetector> _logger;

    private readonly string[] _notSupportedRuntimes =
    {
        "net5.0",
        "netcoreapp3.1",
        "netcoreapp3.0",
        "netcoreapp2.2",
        "netcoreapp2.1",
        "netcoreapp1.1",
        "netcoreapp1.0"
    };

    private readonly string _suggestedRuntime = "net6.0";

    public MissingPropertyDetector(
        ProjectTypeDetector projectTypeDetector,
        ILogger<MissingPropertyDetector> logger)
    {
        _projectTypeDetector = projectTypeDetector;
        _logger = logger;
    }

    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        await Task.CompletedTask;
        foreach (var project in context.AllProjects)
        {
            if (string.IsNullOrWhiteSpace(project.Nullable) && _enforceNullable)
                yield return new MissingProperty(project, nameof(project.Nullable), "enable");

            // Help upgrade old web projects.
            if (
                project.PackageReferences.Any(p => p.Name == "Microsoft.AspNetCore.App") ||
                project.PackageReferences.Any(p => p.Name == "Microsoft.AspNetCore.All") // Is an old Web Project.
            )
            {
                if (project.PackageReferences.FirstOrDefault(p => p.Name == "Microsoft.AspNetCore.App") is not null)
                    yield return new ObsoletePackageReference(project, "Microsoft.AspNetCore.App");
                if (project.PackageReferences.FirstOrDefault(p => p.Name == "Microsoft.AspNetCore.All") is not null)
                    yield return new ObsoletePackageReference(project, "Microsoft.AspNetCore.All");
                if (project.PackageReferences.FirstOrDefault(p => p.Name == "Microsoft.AspNetCore.Razor.Design") is not
                    null)
                    yield return new ObsoletePackageReference(project, "Microsoft.AspNetCore.Razor.Design");

                if (project.Sdk?.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == false)
                    yield return new InsertFrameworkReference(project, "Microsoft.AspNetCore.App");
            }

            _logger.LogTrace("Analysing project {Project} with detector...", project);
            var projectInfo = _projectTypeDetector.Detect(project);

            // Output type.
            var outputType = 
                projectInfo.IsWindowsExecutable ? "WinExe" :
                projectInfo.IsExecutable ? "Exe" : "Library";
            if (project.OutputType != outputType)
            {
                _logger.LogTrace("Project {Project} is missing property OutputType", project);
                yield return new MissingProperty(project, nameof(project.OutputType), outputType);
            }

            // Target framework.
            var versionSuggestion = AnalyzeVersion(project);
            if (versionSuggestion != null) yield return versionSuggestion;

            // Assembly name.
            if (string.IsNullOrWhiteSpace(project.AssemblyName) && !projectInfo.IsUnitTest)
            {
                var assemblyName = projectInfo.ShouldPackAsNugetTool ? GenerateExecutableFileName(project.FileName) : project.FileName;
                _logger.LogTrace("Project {Project} is missing property AssemblyName", project);
                yield return new MissingProperty(project, nameof(project.AssemblyName), assemblyName);
            }
            
            // Root namespace.
            if (string.IsNullOrWhiteSpace(project.RootNamespace))
            {
                var rootNamespace = project.FileName.Replace("-", string.Empty);
                _logger.LogTrace("Project {Project} is missing property RootNamespace", project);
                yield return new MissingProperty(project, nameof(project.RootNamespace), rootNamespace);
            }

            // Is test project
            if (project.IsTestProject != projectInfo.IsUnitTest.ToString().ToLower())
            {
                _logger.LogTrace("Project {Project} is missing property IsTestProject", project);
                yield return new MissingProperty(project, nameof(project.IsTestProject), projectInfo.IsUnitTest.ToString().ToLower());
            }
            
            // Is Packable
            if (project.IsPackable != projectInfo.ShouldPackAsNugetLibrary.ToString().ToLower())
            {
                _logger.LogTrace("Project {Project} is missing property IsPackable", project);
                yield return new MissingProperty(project, nameof(project.IsPackable), projectInfo.ShouldPackAsNugetLibrary.ToString().ToLower());
            }
            
            // GeneratePackageOnBuild
            if (projectInfo.ShouldPackAsNugetLibrary && project.GeneratePackageOnBuild.IsFalse())
            {
                _logger.LogTrace("Project {Project} is missing property GeneratePackageOnBuild", project);
                yield return new MissingProperty(project, nameof(project.GeneratePackageOnBuild), true.ToString().ToLower());
            }

            if (projectInfo.ShouldPackAsNugetTool)
            {
                // Pack as tool
                if (project.PackAsTool.IsFalse())
                {
                    _logger.LogTrace("Project {Project} is missing property PackAsTool", project);
                    yield return new MissingProperty(project, nameof(project.PackAsTool), projectInfo.ShouldPackAsNugetTool.ToString().ToLower());
                }
            
                // Tool command name
                if (project.ToolCommandName != project.AssemblyName)
                {
                    var assemblyName = GenerateExecutableFileName(project.FileName);
                    _logger.LogTrace("Project {Project} is missing property ToolCommandName", project);
                    yield return new MissingProperty(project, nameof(project.ToolCommandName), assemblyName);
                }
            }
            
            // Implicit using
            if (project.ImplicitUsings.IsFalse())
            {
                _logger.LogTrace("Project {Project} is missing property Implicit using", project);
                yield return new MissingProperty(project, nameof(project.ImplicitUsings), "enable");
            }

            if (projectInfo.ShouldPackAsNugetLibrary)
            {
                // Company
                if (string.IsNullOrWhiteSpace(project.Company))
                {
                    var company = project.FileName.Split('.').First();
                    _logger.LogTrace("Project {Project} is missing property Company", project);
                    yield return new MissingProperty(project, nameof(project.Company), company);
                }
                
                // Product
                if (string.IsNullOrWhiteSpace(project.Product))
                {
                    var product = project.FileName.Split('.').Last();
                    _logger.LogTrace("Project {Project} is missing property Product", project);
                    yield return new MissingProperty(project, nameof(project.Product), product);
                }
                
                // Description
                if (string.IsNullOrWhiteSpace(project.Description))
                {
                    var company = project.FileName.Split('.').First();
                    var product = project.FileName.Split('.').Last();
                    _logger.LogTrace("Project {Project} is missing property Description", project);
                    yield return new MissingProperty(project, nameof(project.Description), $"Nuget package of '{product}' provided by {company}");
                }
                
                // PackageId
                if (project.PackageId != project.FileName)
                {
                    _logger.LogTrace("Project {Project} is missing property PackageId", project);
                    yield return new MissingProperty(project, nameof(project.PackageId), project.FileName);
                }
                
                // PackageTags
                if (string.IsNullOrWhiteSpace(project.PackageTags))
                {
                    var tags = "nuget package dotnet csproj dependencies";
                    if (projectInfo.ShouldPackAsNugetTool)
                    {
                        tags = "nuget package dotnet cli tool";
                    }
                    _logger.LogTrace("Project {Project} is missing property PackageTags", project);
                    yield return new MissingProperty(project, nameof(project.PackageTags), tags);
                }
            }
            
            _logger.LogTrace("Project {Project} analyse finished", project);
        }
    }

    private ResetRuntime? AnalyzeVersion(Project project)
    {
        var runtimes = project.GetTargetFrameworks();
        for (var i = 0; i < runtimes.Length; i++)
            foreach (var notSupportedRuntime in _notSupportedRuntimes)
                if (runtimes[i].Contains(notSupportedRuntime, StringComparison.OrdinalIgnoreCase))
                    runtimes[i] = runtimes[i].ToLower().Replace(notSupportedRuntime, _suggestedRuntime);

        var cleanedRuntimes = runtimes.Select(r => r.ToLower().Trim()).Distinct().ToArray();

        var deprecatedCount = project.GetTargetFrameworks().Except(cleanedRuntimes).Count();
        var insertedCount = cleanedRuntimes.Except(project.GetTargetFrameworks()).Count();
        if (deprecatedCount > 0 || insertedCount > 0)
            return new ResetRuntime(project, cleanedRuntimes, insertedCount, deprecatedCount);
        return null;
    }

    private static string GenerateExecutableFileName(string projectName)
    {
        string[] nameParts = projectName.Split('.');
        string lastName = nameParts[^1];

        string fileName = "";
        for (int i = 0; i < lastName.Length; i++)
        {
            if (char.IsUpper(lastName[i]))
            {
                if (i > 0)
                {
                    fileName += "-";
                }

                fileName += char.ToLower(lastName[i]);
            }
            else
            {
                fileName += lastName[i];
            }
        }

        return fileName;
    }
}