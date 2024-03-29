﻿using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.Core.Services.Nuget.Models;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin.Services;

public class DeprecatedPackageDetector : IActionDetector
{
    private readonly ILogger<DeprecatedPackageDetector> _logger;
    private readonly NugetService _nugetService;

    public DeprecatedPackageDetector(
        ILogger<DeprecatedPackageDetector> logger,
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
            CatalogInformation? catalogInformation;
            try
            {
                catalogInformation = await _nugetService.GetPackageDeprecationInfo(package);
            }
            catch (Exception e)
            {
                _logger.LogTrace(e, "Failed to get package deprecation info by name: \'{Package}\'", package);
                _logger.LogCritical("Failed to get package deprecation info by name: \'{Package}\'", package);
                continue;
            }

            if (catalogInformation?.Deprecation != null)
            {
                Package? alternative = null;
                if (!string.IsNullOrWhiteSpace(catalogInformation.Deprecation.AlternatePackage?.Id))
                {
                    var alternativeVersion =
                        await _nugetService.GetLatestVersion(catalogInformation.Deprecation.AlternatePackage.Id, project.GetTargetFrameworks());
                    alternative = new Package(catalogInformation.Deprecation.AlternatePackage.Id, alternativeVersion);
                }

                yield return new DeprecatedPackageReplacement(
                    project,
                    package,
                    alternative);
            }
            else if (catalogInformation?.Vulnerabilities?.Any() == true)
            {
                yield return new VulnerablePackageReplacement(project, package);
            }
        }
    }
}