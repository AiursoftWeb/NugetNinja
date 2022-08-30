using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class ResetSdkLevel : IAction
{
    public Project Project { get; }
    public string NewSdk { get; }

    public ResetSdkLevel(Project project, string newSdk)
    {
        Project = project;
        NewSdk = newSdk;
    }

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
