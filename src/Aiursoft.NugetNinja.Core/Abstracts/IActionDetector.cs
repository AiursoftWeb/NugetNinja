namespace Aiursoft.NugetNinja.Core.Abstracts;

public interface IActionDetector
{
    public IAsyncEnumerable<IAction> AnalyzeAsync(Model.Workspace.Model context);
}