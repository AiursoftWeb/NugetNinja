using Aiursoft.NugetNinja.Core;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin;
using Aiursoft.NugetNinja.MissingPropertyPlugin;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class RunAllOfficialPluginsService : IEntryService
{
    private readonly Extractor _extractor;
    private readonly ILogger<RunAllOfficialPluginsService> _logger;
    private readonly List<IActionDetector> _pluginDetectors;

    public RunAllOfficialPluginsService(
        ILogger<RunAllOfficialPluginsService> logger,
        Extractor extractor,
        MissingPropertyDetector missingPropertyDetector,
        DeprecatedPackageDetector deprecatedPackageDetector,
        PackageReferenceUpgradeDetector packageReferenceUpgradeDetector,
        UselessPackageReferenceDetector uselessPackageReferenceDetector,
        UselessProjectReferenceDetector uselessProjectReferenceDetector)
    {
        _logger = logger;
        _extractor = extractor;
        _pluginDetectors = new List<IActionDetector>
        {
            missingPropertyDetector,
            uselessPackageReferenceDetector,
            uselessProjectReferenceDetector,
            packageReferenceUpgradeDetector,
            deprecatedPackageDetector
        };
    }

    public async Task OnServiceStartedAsync(string path, bool shouldTakeAction)
    {
        foreach (var plugin in _pluginDetectors)
        {
            _logger.LogTrace("Parsing files to build project structure based on path: \'{Path}\'...", path);
            var model = await _extractor.Parse(path);

            _logger.LogInformation("Analyzing possible actions via {Name}...", plugin.GetType().Name);
            var actions = plugin.AnalyzeAsync(model);

            await foreach (var action in actions)
            {
                _logger.LogWarning("Action {Action} built suggestion: {Suggestion}", action.GetType().Name, action.BuildMessage());
                if (shouldTakeAction) await action.TakeActionAsync();
            }
        }
    }
}