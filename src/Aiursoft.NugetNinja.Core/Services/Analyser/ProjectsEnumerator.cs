using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.Core.Services.Analyser;

public class ProjectsEnumerator
{
    public IEnumerable<Project> EnumerateAllBuiltProjects(Project input, bool includeSelf = true)
    {
        if (includeSelf) yield return input;
        foreach (var subProject in input.ProjectReferences)
        foreach (var result in EnumerateAllBuiltProjects(subProject))
            yield return result;
    }
}