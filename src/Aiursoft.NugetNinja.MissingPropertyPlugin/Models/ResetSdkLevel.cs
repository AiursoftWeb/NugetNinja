using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class ResetSdkLevel : IAction
{
    public ResetSdkLevel(Project project, string newSdk)
    {
        SourceProject = project;
        NewSdk = newSdk;
    }

    public Project SourceProject { get; }
    public string NewSdk { get; }

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