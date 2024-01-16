using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin.Models;

public class IncreaseVersionAction : IAction
{
    public IncreaseVersionAction(Project source, NugetVersion newVersion)
    {
        SourceProject = source;
        NewVersion = newVersion;
    }

    public Project SourceProject { get; }
    public NugetVersion NewVersion { get; }

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' should release a new version because it was updated!";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.AddOrUpdateProperty("Version", NewVersion.ToString());
    }
}