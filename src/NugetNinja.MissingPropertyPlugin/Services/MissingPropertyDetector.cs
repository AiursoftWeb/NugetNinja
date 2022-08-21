﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.NugetNinja.Core;

namespace Microsoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyDetector : IActionDetector
{
    private readonly ILogger<MissingPropertyDetector> _logger;

    public MissingPropertyDetector(
        ILogger<MissingPropertyDetector> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        await Task.CompletedTask;
        foreach (var project in context.AllProjects)
        {
            if (project.Sdk?.EndsWith("Web") ?? project.OutputType?.EndsWith("exe") ?? false)
            {
                _logger.LogTrace($"Skip scanning properties for project: '{project}' because it's an executable.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(project.OutputType))
                yield return new MissingProperty(project, nameof(project.OutputType), "Library");
            if (string.IsNullOrWhiteSpace(project.TargetFramework) || string.Compare(project.TargetFramework, "net6.0", StringComparison.Ordinal) < 0)
                yield return new MissingProperty(project, nameof(project.TargetFramework), "net6.0");
            if (string.IsNullOrWhiteSpace(project.Nullable))
                yield return new MissingProperty(project, nameof(project.Nullable), "enable");
            if (string.IsNullOrWhiteSpace(project.ImplicitUsings))
                yield return new MissingProperty(project, nameof(project.ImplicitUsings), "enable");
            if (string.IsNullOrWhiteSpace(project.Description))
                yield return new MissingProperty(project, nameof(project.Description), "A library that shared to nuget.");
            if (string.IsNullOrWhiteSpace(project.Version))
                yield return new MissingProperty(project, nameof(project.Version), "0.0.1");
            if (string.IsNullOrWhiteSpace(project.Company))
                yield return new MissingProperty(project, nameof(project.Company), "Contoso");
            if (string.IsNullOrWhiteSpace(project.Product))
                yield return new MissingProperty(project, nameof(project.Product), project.ToString());
            if (string.IsNullOrWhiteSpace(project.Authors))
                yield return new MissingProperty(project, nameof(project.Authors), $"{project}Team");
            if (string.IsNullOrWhiteSpace(project.PackageTags))
                yield return new MissingProperty(project, nameof(project.PackageTags), $"nuget tools extensions");
            if (string.IsNullOrWhiteSpace(project.PackageProjectUrl))
                yield return new MissingProperty(project, nameof(project.PackageProjectUrl), $"https://github.com/SampleOrg/SampleRepo");
            if (string.IsNullOrWhiteSpace(project.RepositoryUrl))
                yield return new MissingProperty(project, nameof(project.RepositoryUrl), $"https://github.com/SampleOrg/SampleRepo");
            if (string.IsNullOrWhiteSpace(project.RepositoryType))
                yield return new MissingProperty(project, nameof(project.RepositoryType), $"git");
        }
    }
}
