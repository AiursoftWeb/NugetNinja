using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class UselessProjectReference : IAction
{
    public UselessProjectReference(Project source, Project target)
    {
        SourceProjectName = source;
        TargetProjectName = target;
    }

    public Project SourceProjectName { get; set; }
    public Project TargetProjectName { get; set; }

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProjectName}' don't have to reference project '{TargetProjectName}' because it already has its access via another path!";
    }

    public Task TakeActionAsync()
    {
        return SourceProjectName.RemoveProjectReference(TargetProjectName.PathOnDisk);
    }
}