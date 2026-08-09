using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.Logging;
using WorkspaceModel = Aiursoft.NugetNinja.Core.Model.Workspace.Model;

namespace Aiursoft.NugetNinja.Core.Services.Analyser;

public enum TransitiveSecurityOverrideState
{
    Confirmed,
    Indeterminate
}

public sealed record TransitiveSecurityOverride(
    Project Project,
    Package DirectReference,
    Package? TransitiveReference,
    TransitiveSecurityOverrideState State);

/// <summary>
/// Finds direct references that exist to keep a lower transitive dependency tree
/// away from known vulnerabilities. Any incomplete dependency or vulnerability
/// information is reported as indeterminate so the upgrade pass can fail closed.
/// </summary>
public class TransitiveSecurityOverrideService(
    ILogger<TransitiveSecurityOverrideService> logger,
    NugetService nugetService,
    ProjectsEnumerator projectsEnumerator)
{
    private const int MaxPackagesPerSafetyWalk = 64;

    public async Task<IReadOnlyCollection<TransitiveSecurityOverride>> FindOverridesAsync(WorkspaceModel context)
    {
        var results = new List<TransitiveSecurityOverride>();
        foreach (var project in context.AllProjects)
        {
            var dependencyRoots = project.PackageReferences
                .Concat(projectsEnumerator
                    .EnumerateAllBuiltProjects(project, false)
                    .SelectMany(referencedProject => referencedProject.PackageReferences))
                .DistinctBy(package => PackageKey(package))
                .ToArray();

            var directDependencies = new Dictionary<string, Package[]>(StringComparer.OrdinalIgnoreCase);
            var dependencyGraphComplete = true;
            foreach (var root in dependencyRoots)
            {
                try
                {
                    directDependencies[PackageKey(root)] = await nugetService.GetPackageDependencies(root);
                }
                catch (Exception exception)
                {
                    dependencyGraphComplete = false;
                    logger.LogWarning(exception,
                        "Unable to inspect dependencies of {Package} {Version}; package upgrades in {Project} will fail closed.",
                        root.Name, root.Version, project);
                }
            }

            if (!dependencyGraphComplete)
            {
                results.AddRange(project.PackageReferences.Select(package =>
                    new TransitiveSecurityOverride(
                        project,
                        package,
                        null,
                        TransitiveSecurityOverrideState.Indeterminate)));
                continue;
            }

            foreach (var directReference in project.PackageReferences)
            {
                var alternatives = new List<Package>();
                foreach (var referencedProject in projectsEnumerator.EnumerateAllBuiltProjects(project, false))
                {
                    alternatives.AddRange(referencedProject.PackageReferences);
                }

                foreach (var root in dependencyRoots.Where(root => !ReferenceEquals(root, directReference)))
                {
                    alternatives.AddRange(directDependencies[PackageKey(root)]);
                }

                var transitiveReference = alternatives
                    .Where(package => string.Equals(
                        package.Name,
                        directReference.Name,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(package => package.Version)
                    .FirstOrDefault();

                if (transitiveReference == null || transitiveReference.Version >= directReference.Version)
                {
                    continue;
                }

                try
                {
                    var transitiveTreeIsVulnerable = await HasKnownVulnerabilityInClosureAsync(transitiveReference);
                    if (transitiveTreeIsVulnerable &&
                        (await GetKnownVulnerabilityIdsInClosureAsync(directReference)).Count == 0)
                    {
                        results.Add(new TransitiveSecurityOverride(
                            project,
                            directReference,
                            transitiveReference,
                            TransitiveSecurityOverrideState.Confirmed));
                    }
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception,
                        "Unable to verify whether {Package} {Version} is a transitive security override; its version will be frozen.",
                        directReference.Name, directReference.Version);
                    results.Add(new TransitiveSecurityOverride(
                        project,
                        directReference,
                        transitiveReference,
                        TransitiveSecurityOverrideState.Indeterminate));
                }
            }
        }

        return results;
    }

    public async Task<IReadOnlySet<string>> GetKnownVulnerabilityIdsInClosureAsync(Package root)
    {
        var vulnerabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new Queue<Package>();
        packages.Enqueue(root);

        while (packages.TryDequeue(out var package))
        {
            if (!visited.Add(PackageKey(package)))
            {
                continue;
            }

            EnsureSafetyWalkIsBounded(visited.Count);
            var packageVulnerabilities = await nugetService.GetKnownVulnerabilities(package);
            foreach (var vulnerability in packageVulnerabilities)
            {
                vulnerabilities.Add(vulnerability.AdvisoryUrl);
            }

            var dependencies = await nugetService.GetPackageDependencies(package);
            foreach (var dependency in dependencies)
            {
                packages.Enqueue(dependency);
            }
        }

        return vulnerabilities;
    }

    private async Task<bool> HasKnownVulnerabilityInClosureAsync(Package root)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packages = new Queue<Package>();
        packages.Enqueue(root);

        while (packages.TryDequeue(out var package))
        {
            if (!visited.Add(PackageKey(package)))
            {
                continue;
            }

            EnsureSafetyWalkIsBounded(visited.Count);
            var vulnerabilities = await nugetService.GetKnownVulnerabilities(package);
            if (vulnerabilities.Count > 0)
            {
                return true;
            }

            var dependencies = await nugetService.GetPackageDependencies(package);
            foreach (var dependency in dependencies)
            {
                packages.Enqueue(dependency);
            }
        }

        return false;
    }

    private static void EnsureSafetyWalkIsBounded(int visitedPackageCount)
    {
        if (visitedPackageCount > MaxPackagesPerSafetyWalk)
        {
            throw new InvalidOperationException(
                $"The dependency safety walk exceeded {MaxPackagesPerSafetyWalk} packages.");
        }
    }

    private static string PackageKey(Package package) => $"{package.Name}/{package.Version}";
}
