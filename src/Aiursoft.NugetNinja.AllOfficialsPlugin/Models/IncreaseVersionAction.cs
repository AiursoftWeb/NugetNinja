using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin.Models;

public class IncreaseVersionAction(Project source, NugetVersion newVersion) : IAction
{
    public Project SourceProject { get; } = source;
    public NugetVersion NewVersion { get; } = newVersion;

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