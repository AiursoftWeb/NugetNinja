using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.DuplicatePropertyPlugin.Models;

public class DuplicateProperty(Project project, string propertyName, string correctValue) : IAction
{
    public Project SourceProject { get; } = project;

    public string BuildMessage()
    {
        return $"The project: {SourceProject} has duplicate property: {propertyName}. It should be unique.";
    }

    public async Task TakeActionAsync()
    {
        // Combine remove + add into a single file read-modify-save cycle
        // to avoid multiple CsprojWriter.SaveCsprojToDisk calls that can
        // compound XML formatting issues.
        await SourceProject.DeduplicateProperty(propertyName, correctValue);
    }
}