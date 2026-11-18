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
    TransitiveSecurityOverrideService securityOverrideService,
    NpmPackageUpgradeDetector npmPackageUpgradeDetector)
    : IActionDetector
{
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        var securityOverrides = await securityOverrideService.FindOverridesAsync(context);

        foreach (var project in context.AllProjects)
        {
            var frozen = securityOverrides.Where(overrideEntry =>
                overrideEntry.Project.PathOnDisk == project.PathOnDisk).ToArray();

            foreach (var (name, members) in project.SameVersionGroups)
            {
                var groupUpgrade = await FindGroupUpgradeAsync(project, name, members, frozen);
                if (groupUpgrade != null)
                    yield return groupUpgrade;
            }

            foreach (var package in project.PackageReferences.Where(package => !project.IsGroupedPackage(package.Name)))
            {
                var securityOverride = frozen.FirstOrDefault(candidate =>
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

        await foreach (var action in npmPackageUpgradeDetector.AnalyzeAsync(context))
        {
            yield return action;
        }
    }
    private async Task<PackageGroupUpgrade?> FindGroupUpgradeAsync(Project project, string name,
        string[] members, TransitiveSecurityOverride[] frozen)
    {
        if (!project.CanUpdatePackageGroup(members))
        {
            logger.LogWarning("Keeping NuGet group {Group} in {Project}: a reference is conditional, repeated, or lacks a literal version.",
                name, project);
            return null;
        }

        if (frozen.Any(entry => members.Contains(entry.DirectReference.Name, StringComparer.OrdinalIgnoreCase)))
        {
            logger.LogWarning("Keeping NuGet group {Group} in {Project}: a member is held by an existing security override.",
                name, project);
            return null;
        }

        var packages = project.PackageReferences.Where(package =>
            members.Contains(package.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
        var floor = packages.Max(package => package.Version);
        HashSet<NugetVersion>? common = null;
        try
        {
            var runtimes = project.GetTargetFrameworks();
            foreach (var package in packages)
            {
                var versions = await nugetService.GetStableGroupVersions(package.Name, runtimes);
                if (common == null)
                    common = versions.ToHashSet();
                else
                    common.IntersectWith(versions);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Keeping NuGet group {Group} in {Project}: package versions could not be read.",
                name, project);
            return null;
        }

        if (common == null || common.Count == 0 || common.Max() < floor)
        {
            logger.LogInformation("Keeping NuGet group {Group} in {Project}: no common version can be selected without a downgrade.",
                name, project);
            return null;
        }

        var target = common.Max();
        return packages.Any(package => package.Version < target)
            ? new PackageGroupUpgrade(project, name, members, target)
            : null;
    }
}
