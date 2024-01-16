using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;

public class PossiblePackageUpgrade : IAction
{
    public PossiblePackageUpgrade(Project source, Package target, NugetVersion newVersion)
    {
        SourceProject = source;
        Package = target;
        NewVersion = newVersion;
    }

    public Project SourceProject { get; }
    public Package Package { get; }
    public NugetVersion NewVersion { get; }

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