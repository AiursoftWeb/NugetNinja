using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.Core.Model.Framework;

public class DetectorStarter<T>(
    ILogger<DetectorStarter<T>> logger,
    Extractor extractor,
    T detector)
    : IEntryService
    where T : IActionDetector
{
    private readonly T _detector = detector;

    public async Task OnServiceStartedAsync(string path, bool shouldTakeAction)
    {
        logger.LogInformation("Parsing files to build project structure based on path: \'{Path}\'...", path);
        var model = await extractor.Parse(path);

        logger.LogInformation("Analysing possible actions via {Name}", typeof(T).Name);
        var actions = _detector.AnalyzeAsync(model);
        await foreach (var action in actions)
        {
            logger.LogWarning("Action {Action} built suggestion: {Suggestion}", action.GetType().Name, action.BuildMessage());
            if (shouldTakeAction) await action.TakeActionAsync();
        }
    }
}