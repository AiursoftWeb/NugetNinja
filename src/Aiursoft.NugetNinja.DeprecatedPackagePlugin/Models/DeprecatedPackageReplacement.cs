using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin.Models;

public class DeprecatedPackageReplacement : IAction
{
    public DeprecatedPackageReplacement(Project source, Package target, Package? alternative)
    {
        SourceProject = source;
        Package = target;
        Alternative = alternative;
    }

    public Project SourceProject { get; }
    public Package Package { get; }
    public Package? Alternative { get; }

    public string BuildMessage()
    {
        var alternativeText =
            Alternative != null ? string.Empty : $"Please consider to replace that to: '{Alternative}'.";
        return
            $"The project: '{SourceProject}' referenced a deprecated package: {Package} {Package.Version}! {alternativeText}";
    }

    public async Task TakeActionAsync()
    {
        if (Alternative != null)
            await SourceProject.ReplacePackageReferenceAsync(Package.Name, Alternative);
    }
}