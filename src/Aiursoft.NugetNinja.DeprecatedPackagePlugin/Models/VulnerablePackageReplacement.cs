using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin.Models;

public class VulnerablePackageReplacement : IAction
{
    public VulnerablePackageReplacement(Project source, Package target)
    {
        SourceProject = source;
        Package = target;
    }

    public Project SourceProject { get; }
    public Package Package { get; }

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