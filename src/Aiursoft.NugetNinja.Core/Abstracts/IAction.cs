using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.Core.Abstracts;

public interface IAction
{
    public string BuildMessage();

    public Task TakeActionAsync();

    public Project SourceProject { get; }
}