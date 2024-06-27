using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin.Models;

public class UselessProjectReference(Project source, Project target) : IAction
{
    public Project SourceProject { get; set; } = source;
    public Project TargetProject { get; set; } = target;

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' don't have to reference project '{TargetProject}' because it already has its access via another path!";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.RemoveProjectReference(TargetProject.PathOnDisk);
    }
}