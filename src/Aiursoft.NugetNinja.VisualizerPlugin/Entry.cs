using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Nuget;

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

    public async Task OnServiceStartedAsync(string path, int depth, string[] excludes, bool localOnly)
    {
        var colorShouldBe = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\nTo visualize the dependency tree, please open: https://mermaid.live/ and paste the following content to the editor:");
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(@"
---
title: Project dependency diagram
---

");
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.WriteLine("stateDiagram-v2");
        Console.ForegroundColor = ConsoleColor.Blue;
        var model = await _extractor.Parse(path);
        await foreach (var result in GenerateRelationships(model, depth, excludes, localOnly).Distinct())
        {
            Console.WriteLine(result);
        }
        
        Console.ForegroundColor = colorShouldBe;
    }

    private async IAsyncEnumerable<string> GenerateRelationships(Model model, int depth, string[] excludes, bool localOnly)
    {
        foreach (var project in model.AllProjects.Where(p => !excludes.Any(e => p.FileName.Contains(e))))
        {
            foreach (var pr in project.ProjectReferences.Where(p => !excludes.Any(e => p.FileName.Contains(e))))
            {
                yield return $"    {project.FileName} --> {pr.FileName}";
            }
            if (localOnly) continue;

            foreach (var pr in project.PackageReferences.Where(p => !excludes.Any(e => p.Name.Contains(e))))
            {
                yield return $"    {project.FileName} --> {pr.Name}";
                await foreach (var generated in WritePackageInfo(pr, excludes, currentDepth: 1, maxDepth: depth))
                    yield return generated;
            }
        }
    }

    private async IAsyncEnumerable<string> WritePackageInfo(
        Package package,
        string[] excludes,
        int currentDepth = 1,
        int maxDepth = 2)
    {
        if (currentDepth >= maxDepth)
        {
            yield break;
        }

        var refs = await _nugetService.GetPackageDependencies(package);
        foreach (var reference in refs?.Where(p => !excludes.Any(e => p.Name.Contains(e))) ?? Array.Empty<Package>())
        {
            yield return $"    {package.Name} --> {reference.Name}";
            await foreach (var generated in WritePackageInfo(
                               reference,
                               excludes,
                               currentDepth + 1,
                               maxDepth))
            {
                yield return generated;
            }
        }
    }
}