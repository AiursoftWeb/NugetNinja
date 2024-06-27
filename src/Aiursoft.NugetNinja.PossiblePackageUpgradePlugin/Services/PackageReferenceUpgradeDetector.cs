using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;

public class PackageReferenceUpgradeDetector(
    ILogger<PackageReferenceUpgradeDetector> logger,
    NugetService nugetService)
    : IActionDetector
{
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        foreach (var project in context.AllProjects)
        foreach (var package in project.PackageReferences)
        {
            NugetVersion? latest;
            try
            {
                latest = await nugetService.GetLatestVersion(package.Name, project.GetTargetFrameworks());
            }
            catch (Exception e)
            {
                logger.LogTrace(e, "Failed to get package latest version by name: \'{Package}\'", package);
                logger.LogCritical("Failed to get package latest version by name: \'{Package}\'", package);
                continue;
            }

            if (package.Version < latest) yield return new PossiblePackageUpgrade(project, package, latest);
        }
    }
}