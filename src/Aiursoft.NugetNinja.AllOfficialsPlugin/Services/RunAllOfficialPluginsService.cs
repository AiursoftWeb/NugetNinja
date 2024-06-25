using Aiursoft.NugetNinja.AllOfficialsPlugin.Models;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.Services;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Services;
using Aiursoft.NugetNinja.MissingPropertyPlugin.Services;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Services;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin.Services;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin.Services;

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
        UselessProjectReferenceDetector uselessProjectReferenceDetector,
        ExpectFilesDetector expectFilesDetector)
    {
        _logger = logger;
        _extractor = extractor;
        _pluginDetectors =
        [
            missingPropertyDetector,
            deprecatedPackageDetector,
            packageReferenceUpgradeDetector,
            uselessPackageReferenceDetector,
            uselessProjectReferenceDetector,
            expectFilesDetector
        ];
    }

    public Task OnServiceStartedAsync(string path, bool shouldTakeAction) => RunAllPlugins(path, shouldTakeAction, false);

    public async Task RunAllPlugins(string path, bool shouldTakeAction, bool onlyRunUpdatePlugin)
    {
        var allActionsTaken = new List<IAction>();
        foreach (var plugin in _pluginDetectors)
        {
            if (onlyRunUpdatePlugin && plugin.GetType() != typeof(PackageReferenceUpgradeDetector))
            {
                continue;
            }

            _logger.LogTrace("Parsing files to build project structure based on path: \'{Path}\'...", path);
            var model = await _extractor.Parse(path);

            _logger.LogInformation("Analyzing possible actions via {Name}...", plugin.GetType().Name);
            var actions = plugin.AnalyzeAsync(model);

            await foreach (var action in actions)
            {
                allActionsTaken.Add(action);
                _logger.LogWarning("Action {Action} built suggestion: {Suggestion}", action.GetType().Name, action.BuildMessage());
                if (shouldTakeAction) await action.TakeActionAsync();
            }
        }

        if (!shouldTakeAction)
        {
            return;
        }
        
        var finalModel = await _extractor.Parse(path);
        var projectsShouldUpgrade = finalModel.AllProjects
            .Where(project => !string.IsNullOrWhiteSpace(project.Version))
            .Where(project => HasActionTaken(project, allActionsTaken))
            .ToList();

        foreach (var projectTakenActions in projectsShouldUpgrade)
        {
            if (!string.IsNullOrWhiteSpace(projectTakenActions.Version))
            {
                var increasedVersion = Increase(projectTakenActions.Version);
                var increaseVersionAction = new IncreaseVersionAction(projectTakenActions, increasedVersion);
                _logger.LogWarning("Action {Action} built suggestion: {Suggestion}", increaseVersionAction.GetType().Name, increaseVersionAction.BuildMessage());
                await increaseVersionAction.TakeActionAsync();
            }
        }
    }

    private static NugetVersion Increase(string versionInProject)
    {
        var parsedVersion = new NugetVersion(versionInProject);
        var addedVersion = new Version(
            major: parsedVersion.PrimaryVersion.Major,
            minor: parsedVersion.PrimaryVersion.Minor,
            build: parsedVersion.PrimaryVersion.Build + 1);
        var increasedVersion = new NugetVersion($"{addedVersion}-{parsedVersion.AdditionalText}".TrimEnd('-'));
        return increasedVersion;
    }

    private bool HasActionTaken(Project project, List<IAction> allActions)
    {
        return 
            allActions.Any(a => a.SourceProject?.PathOnDisk == project.PathOnDisk) || 
            project.ProjectReferences.Any(projectReference => HasActionTaken(projectReference, allActions));
    }
}