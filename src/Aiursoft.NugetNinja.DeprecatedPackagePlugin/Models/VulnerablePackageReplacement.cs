using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin.Models;

public class VulnerablePackageReplacement(Project source, Package target) : IAction
{
    public Project SourceProject { get; } = source;
    public Package Package { get; } = target;

    public string BuildMessage()
    {
        return
            $@"The project: '{SourceProject}' referenced a package {Package} {Package.Version} which has known vulnerabilities! Please consider to upgrade\remove\replace it!";
    }

    public Task TakeActionAsync()
    {
        // To DO: Remove this reference.
        throw new NotImplementedException();
    }
}