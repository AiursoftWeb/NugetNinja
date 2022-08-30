using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class ResetSDK : IAction
{
    public Project Project { get; }
    public string NewSDK { get; }

    public ResetSDK(Project project, string newSdk)
    {
        Project = project;
        NewSDK = newSdk;
    }

    public string BuildMessage()
    {
        return $"The project: '{Project}' with SDK: '{Project.Sdk}' should be changed to '{NewSDK}'.";
    }

    public Task TakeActionAsync()
    {
        // To do: Reset the SDK.
        throw new NotImplementedException();
    }
}
