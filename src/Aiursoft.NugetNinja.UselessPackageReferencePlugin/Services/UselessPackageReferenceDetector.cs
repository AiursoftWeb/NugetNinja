using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin.Services;

public class UselessPackageReferenceDetector(
    ILogger<UselessPackageReferenceDetector> logger,
    NugetService nugetService,
    ProjectsEnumerator enumerator)
    : IActionDetector
{
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        foreach (var uselessReferences in context.AllProjects.Select(AnalyzeProject))
        await foreach (var reference in uselessReferences)
            yield return reference;
    }

    private async IAsyncEnumerable<UselessPackageReference> AnalyzeProject(Project context)
    {
        var accessiblePackages = new List<Package>();
        var relatedProjects = enumerator.EnumerateAllBuiltProjects(context, false);
        foreach (var relatedProject in relatedProjects)
        {
            accessiblePackages.AddRange(relatedProject.PackageReferences);
            foreach (var package in relatedProject.PackageReferences)
                try
                {
                    var recursivePackagesBroughtUp = await nugetService.GetPackageDependencies(package);
                    accessiblePackages.AddRange(recursivePackagesBroughtUp);
                }
                catch (Exception e)
                {
                    logger.LogTrace(e, "Failed to get package dependencies by name: \'{Package}\'", package);
                    logger.LogCritical("Failed to get package dependencies by name: \'{Package}\'", package);
                }
        }

        foreach (var directReference in context.PackageReferences)
        {
            var accessiblePackagesForThisProject = accessiblePackages.ToList();
            foreach (var otherDirectReference in context.PackageReferences.Where(p => p != directReference))
                try
                {
                    var references = await nugetService.GetPackageDependencies(otherDirectReference);
                    accessiblePackagesForThisProject.AddRange(references);
                }
                catch (Exception e)
                {
                    logger.LogTrace(e, "Failed to get package dependencies by name: \'{OtherDirectReference}\'", otherDirectReference);
                    logger.LogCritical("Failed to get package dependencies by name: \'{OtherDirectReference}\'", otherDirectReference);
                }

            // A direct reference is not redundant when it pins a package above the version
            // that would otherwise be restored transitively. Removing it in that case can
            // reintroduce vulnerabilities fixed by the newer direct version.
            if (accessiblePackagesForThisProject.Any(pa =>
                    string.Equals(pa.Name, directReference.Name, StringComparison.OrdinalIgnoreCase) &&
                    pa.Version >= directReference.Version))
                yield return new UselessPackageReference(context, directReference);
        }
    }
}
