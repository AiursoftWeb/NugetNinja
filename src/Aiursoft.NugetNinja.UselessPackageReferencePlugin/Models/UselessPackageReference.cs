using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin.Models;

public class UselessPackageReference : IAction
{
    public UselessPackageReference(Project source, Package target)
    {
        SourceProject = source;
        TargetPackage = target;
    }

    public Project SourceProject { get; set; }
    public Package TargetPackage { get; set; }

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' don't have to reference package '{TargetPackage}' because it already has its access via another path!";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.RemovePackageReferenceAsync(TargetPackage.Name);
    }
}