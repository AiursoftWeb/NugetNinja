using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

// ReSharper disable once ClassNeverInstantiated.Global
public class Entry
{
    private readonly NugetService _nugetService;
    private readonly Extractor _extractor;

    public Entry(
        NugetService nugetService,
        Extractor extractor)
    {
        _nugetService = nugetService;
        _extractor = extractor;
    }

    public async Task OnServiceStartedAsync(string path, int depth)
    {
        Console.WriteLine(@"
---
title: Project dependency diagram
---
stateDiagram-v2
");
        var model = await _extractor.Parse(path);
        await foreach (var result in GenerateRelationships(model, depth).Distinct())
        {
            Console.WriteLine(result);
        }
    }

    private async IAsyncEnumerable<string> GenerateRelationships(Model model, int depth)
    {
        foreach (var project in model.AllProjects)
        {
            foreach (var pr in project.ProjectReferences)
            {
                yield return $"    {project.FileName} --> {pr.FileName}";
            }

            foreach (var pr in project.PackageReferences)
            {
                yield return $"    {project.FileName} --> {pr.Name}";
                await foreach (var generated in WritePackageInfo(pr, currentDepth: 1, maxDepth: depth)) yield return generated;
            }
        }
    }

    private async IAsyncEnumerable<string> WritePackageInfo(Package package, int currentDepth = 1, int maxDepth = 2)
    {
        if (currentDepth >= maxDepth)
        {
            yield break;
        }
        var refs = await _nugetService.GetPackageDependencies(package);
        foreach (var reference in refs ?? Array.Empty<Package>())
        {
            yield return $"    {package.Name} --> {reference.Name}";
            await foreach (var generated in WritePackageInfo(reference, currentDepth + 1, maxDepth)) yield return generated;
        }
    }
}