using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;

public class PackageGroupUpgrade(Project project, string group, string[] packages, NugetVersion version) : IAction
{
    public Project SourceProject { get; } = project;

    public string BuildMessage() =>
        $"Upgrade NuGet group '{group}' in '{SourceProject}' to '{version}': {string.Join(", ", packages)}.";

    public Task TakeActionAsync() => SourceProject.SetPackageGroupVersionAsync(packages, version);
}
