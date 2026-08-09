using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;

public class PackageReferenceUpgradeDetector(
    ILogger<PackageReferenceUpgradeDetector> logger,
    NugetService nugetService,
    TransitiveSecurityOverrideService securityOverrideService)
    : IActionDetector
{
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        var securityOverrides = await securityOverrideService.FindOverridesAsync(context);

        foreach (var project in context.AllProjects)
        foreach (var package in project.PackageReferences)
        {
            var securityOverride = securityOverrides.FirstOrDefault(candidate =>
                candidate.Project.PathOnDisk == project.PathOnDisk &&
                string.Equals(
                    candidate.DirectReference.Name,
                    package.Name,
                    StringComparison.OrdinalIgnoreCase));
            if (securityOverride != null)
            {
                logger.LogInformation(
                    securityOverride.State == TransitiveSecurityOverrideState.Confirmed
                        ? "Keeping transitive security override {Package} at {Version} until its parent dependency supplies a safe version."
                        : "Keeping {Package} at {Version} because its transitive security status could not be verified.",
                    package.Name,
                    package.Version);
                continue;
            }

            NugetVersion latest;
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
