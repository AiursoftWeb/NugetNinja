using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyDetector : IActionDetector
{
    private readonly bool _enforceImplicitUsings = false;
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
            if (string.IsNullOrWhiteSpace(project.ImplicitUsings) && _enforceImplicitUsings)
                yield return new MissingProperty(project, nameof(project.ImplicitUsings), "enable");

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

            var projectInfo = _projectTypeDetector.Detect(project);

            // Output type.
            if (string.IsNullOrWhiteSpace(project.OutputType) && !projectInfo.IsUnitTest)
            {
                // TODO: Use WinExe for win forms and wpf.
                var outputType = projectInfo.IsExecutable ? "Exe" : "Library";
                yield return new MissingProperty(project, nameof(project.OutputType), outputType);
            }

            // Target framework.
            var versionSuggestion = AnalyzeVersion(project);
            if (versionSuggestion != null) yield return versionSuggestion;

            // Assembly name.
            if (string.IsNullOrWhiteSpace(project.AssemblyName) && !projectInfo.IsUnitTest)
            {
                var assemblyName = projectInfo.IsExecutable ? GenerateFileName(project.FileName) : project.FileName;
                yield return new MissingProperty(project, nameof(project.AssemblyName), assemblyName);
            }
            
            // Root namespace.
            if (string.IsNullOrWhiteSpace(project.RootNamespace))
            {
                var rootNamespace = project.FileName.Replace("-", string.Empty);
                yield return new MissingProperty(project, nameof(project.RootNamespace), rootNamespace);
            }

            // Is test project
            if (string.IsNullOrWhiteSpace(project.IsTestProject))
            {
                yield return new MissingProperty(project, nameof(project.IsTestProject), projectInfo.IsUnitTest.ToString().ToLower());
            }
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

    private static string GenerateFileName(string projectName)
    {
        string[] nameParts = projectName.Split('.');
        string lastName = nameParts[nameParts.Length - 1];

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