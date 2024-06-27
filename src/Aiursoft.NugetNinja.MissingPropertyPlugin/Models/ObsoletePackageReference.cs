using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Models;

public class ObsoletePackageReference(Project source, string target) : IAction
{
    public Project SourceProject { get; set; } = source;
    public string TargetPackage { get; set; } = target;

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' don't have to reference package '{TargetPackage}' because it is deprecated.";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.RemovePackageReferenceAsync(TargetPackage);
    }
}