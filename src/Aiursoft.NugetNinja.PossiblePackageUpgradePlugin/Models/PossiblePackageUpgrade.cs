using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;

public class PossiblePackageUpgrade(Project source, Package target, NugetVersion newVersion) : IAction
{
    public Project SourceProject { get; } = source;
    public Package Package { get; } = target;
    public NugetVersion NewVersion { get; } = newVersion;

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' should upgrade the package '{Package}' from '{Package.SourceVersionText}' to '{NewVersion}'.";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.SetPackageReferenceVersionAsync(Package.Name, NewVersion);
    }
}