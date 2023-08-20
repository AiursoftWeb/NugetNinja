using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.Core;

public class DetectorStarter<T> : IEntryService where T : IActionDetector
{
    private readonly T _detector;
    private readonly Extractor _extractor;
    private readonly ILogger<DetectorStarter<T>> _logger;

    public DetectorStarter(
        ILogger<DetectorStarter<T>> logger,
        Extractor extractor,
        T detector)
    {
        _logger = logger;
        _extractor = extractor;
        _detector = detector;
    }

    public async Task OnServiceStartedAsync(string path, bool shouldTakeAction)
    {
        _logger.LogInformation("Parsing files to build project structure based on path: \'{Path}\'...", path);
        var model = await _extractor.Parse(path);

        _logger.LogInformation("Analysing possible actions via {Name}", typeof(T).Name);
        var actions = _detector.AnalyzeAsync(model);
        await foreach (var action in actions)
        {
            _logger.LogWarning("Action {Action} built suggestion: {Suggestion}", action.GetType().Name, action.BuildMessage());
            if (shouldTakeAction) await action.TakeActionAsync();
        }
    }
}