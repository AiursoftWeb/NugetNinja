namespace Aiursoft.NugetNinja.Core;

public interface IActionDetector
{
    public IAsyncEnumerable<IAction> AnalyzeAsync(Model context);
}