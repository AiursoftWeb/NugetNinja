using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Models;

public class ResetSdkLevel(Project project, string newSdk) : IAction
{
    public Project SourceProject { get; } = project;
    public string NewSdk { get; } = newSdk;

    public string BuildMessage()
    {
        return $"The project: '{SourceProject}' with SDK: '{SourceProject.Sdk}' should be changed to '{NewSdk}'.";
    }

    public Task TakeActionAsync()
    {
        // To do: Reset the SDK.
        throw new NotImplementedException();
    }
}