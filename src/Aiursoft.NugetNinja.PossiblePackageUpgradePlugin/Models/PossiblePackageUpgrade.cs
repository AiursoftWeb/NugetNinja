using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class PossiblePackageUpgrade : IAction
{
    public PossiblePackageUpgrade(Project source, Package target, NugetVersion newVersion)
    {
        SourceProjectName = source;
        Package = target;
        NewVersion = newVersion;
    }

    public Project SourceProjectName { get; }
    public Package Package { get; }
    public NugetVersion NewVersion { get; }

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProjectName}' should upgrade the package '{Package}' from '{Package.SourceVersionText}' to '{NewVersion}'.";
    }

    public Task TakeActionAsync()
    {
        return SourceProjectName.SetPackageReferenceVersionAsync(Package.Name, NewVersion);
    }
}