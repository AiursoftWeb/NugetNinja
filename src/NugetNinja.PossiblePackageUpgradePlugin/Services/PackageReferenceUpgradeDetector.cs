using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class PackageReferenceUpgradeDetector : IActionDetector
{
    private readonly ILogger<PackageReferenceUpgradeDetector> _logger;
    private readonly NugetService _nugetService;

    public PackageReferenceUpgradeDetector(
        ILogger<PackageReferenceUpgradeDetector> logger,
        NugetService nugetService)
    {
        _logger = logger;
        _nugetService = nugetService;
    }

    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        foreach (var project in context.AllProjects)
        foreach (var package in project.PackageReferences)
        {
            NugetVersion? latest;
            try
            {
                latest = await _nugetService.GetLatestVersion(package.Name, project.GetTargetFrameworks());
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, $"Failed to get package latest version by name: '{package}'.");
                _logger.LogCritical($"Failed to get package latest version by name: '{package}'.");
                continue;
            }

            if (package.Version < latest) yield return new PossiblePackageUpgrade(project, package, latest);
        }
    }
}