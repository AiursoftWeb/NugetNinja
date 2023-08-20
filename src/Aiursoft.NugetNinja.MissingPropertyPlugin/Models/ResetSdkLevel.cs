using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class ResetSdkLevel : IAction
{
    public ResetSdkLevel(Project project, string newSdk)
    {
        Project = project;
        NewSdk = newSdk;
    }

    public Project Project { get; }
    public string NewSdk { get; }

    public string BuildMessage()
    {
        return $"The project: '{Project}' with SDK: '{Project.Sdk}' should be changed to '{NewSdk}'.";
    }

    public Task TakeActionAsync()
    {
        // To do: Reset the SDK.
        throw new NotImplementedException();
    }
}