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
        await SourceProject.RemoveProperty(propertyName);
        await SourceProject.AddOrUpdateProperty(propertyName, correctValue);
    }
}