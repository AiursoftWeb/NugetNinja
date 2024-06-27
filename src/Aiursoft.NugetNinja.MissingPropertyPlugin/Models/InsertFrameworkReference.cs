using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Models;

public class InsertFrameworkReference(Project source, string frameworkReference) : IAction
{
    public Project SourceProject { get; set; } = source;
    public string FrameworkReference { get; set; } = frameworkReference;

    public string BuildMessage()
    {
        return $"The project: '{SourceProject}' may need to reference framework: {FrameworkReference}.";
    }

    public Task TakeActionAsync()
    {
        return SourceProject.AddFrameworkReference(FrameworkReference);
    }
}