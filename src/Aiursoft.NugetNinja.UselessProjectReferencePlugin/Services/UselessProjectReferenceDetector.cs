using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin.Models;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin.Services;

public class UselessProjectReferenceDetector(ProjectsEnumerator enumerator) : IActionDetector
{
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        // By making this an async iterator, we manually wrap the synchronous
        // collection and avoid the ambiguous .ToAsyncEnumerable() call.
        foreach (var action in Analyze(context))
        {
            yield return action;
        }
        
        // We add a single await to make the method truly asynchronous
        // and satisfy the compiler/runtime.
        await Task.CompletedTask;
    }

    private IEnumerable<IAction> Analyze(Model context)
    {
        return context.AllProjects.SelectMany(AnalyzeProject);
    }

    private IEnumerable<UselessProjectReference> AnalyzeProject(Project context)
    {
        var directReferences = context.ProjectReferences;

        var allRecursiveReferences = new List<Project>();
        foreach (var recursiveReferences in
                 directReferences.Select(directReference =>
                     enumerator.EnumerateAllBuiltProjects(directReference, false)))
            allRecursiveReferences.AddRange(recursiveReferences);

        foreach (var directReference in directReferences.Where(directReference =>
                     allRecursiveReferences.Contains(directReference)))
            yield return new UselessProjectReference(context, directReference);
    }
}