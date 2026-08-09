using Aiursoft.NugetNinja.AllOfficialsPlugin.Models;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.Services;
using Aiursoft.NugetNinja.DuplicatePropertyPlugin.Services;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Services;
using Aiursoft.NugetNinja.MissingPropertyPlugin.Services;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Models;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Services;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin.Services;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin.Services;

public class RunAllOfficialPluginsService(
    ILogger<RunAllOfficialPluginsService> logger,
    Extractor extractor,
    TransitiveSecurityOverrideService securityOverrideService,
    MissingPropertyDetector missingPropertyDetector,
    DuplicatePropertyDetector duplicatePropertyDetector,
    DeprecatedPackageDetector deprecatedPackageDetector,
    PackageReferenceUpgradeDetector packageReferenceUpgradeDetector,
    UselessPackageReferenceDetector uselessPackageReferenceDetector,
    UselessProjectReferenceDetector uselessProjectReferenceDetector,
    ExpectFilesDetector expectFilesDetector)
    : IEntryService
{
    private readonly List<IActionDetector> _pluginDetectors =
    [
        // Freeze transitive security overrides while upgrading their parents. The
        // cleanup pass reparses the changed projects and retires an override once
        // its parent now supplies an equal or newer version.
        packageReferenceUpgradeDetector,
        uselessPackageReferenceDetector,
        uselessProjectReferenceDetector,
        duplicatePropertyDetector,
        missingPropertyDetector,
        expectFilesDetector,
        deprecatedPackageDetector
    ];

    public Task OnServiceStartedAsync(string path, bool shouldTakeAction) => RunAllPlugins(path, shouldTakeAction, false);

    public async Task RunAllPlugins(string path, bool shouldTakeAction, bool onlyRunUpdatePlugin)
    {
        HashSet<string>? updateOnlySecurityOverrides = null;
        if (onlyRunUpdatePlugin)
        {
            var initialModel = await extractor.Parse(path);
            var overrides = await securityOverrideService.FindOverridesAsync(initialModel);
            updateOnlySecurityOverrides = overrides
                .Where(securityOverride => securityOverride.State == TransitiveSecurityOverrideState.Confirmed)
                .Select(securityOverride => SecurityOverrideKey(
                    securityOverride.Project.PathOnDisk,
                    securityOverride.DirectReference.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var allActionsTaken = new List<IAction>();
        foreach (var plugin in _pluginDetectors)
        {
            if (onlyRunUpdatePlugin &&
                plugin.GetType() != typeof(PackageReferenceUpgradeDetector) &&
                plugin.GetType() != typeof(UselessPackageReferenceDetector))
            {
                continue;
            }

            logger.LogTrace("Parsing files to build project structure based on path: \'{Path}\'...", path);
            var model = await extractor.Parse(path);

            logger.LogInformation("Analyzing possible actions via {Name}...", plugin.GetType().Name);
            var actions = plugin.AnalyzeAsync(model);

            await foreach (var action in actions)
            {
                if (onlyRunUpdatePlugin &&
                    action is UselessPackageReference uselessPackageReference &&
                    !updateOnlySecurityOverrides!.Contains(SecurityOverrideKey(
                        uselessPackageReference.SourceProject.PathOnDisk,
                        uselessPackageReference.TargetPackage.Name)))
                {
                    continue;
                }

                allActionsTaken.Add(action);
                logger.LogWarning("Action {Action} built suggestion: {Suggestion}", action.GetType().Name, action.BuildMessage());
                if (shouldTakeAction && action.IsModifyingAction) await action.TakeActionAsync();
            }
        }

        if (!shouldTakeAction)
        {
            return;
        }
        
        var finalModel = await extractor.Parse(path);
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
                logger.LogWarning("Action {Action} built suggestion: {Suggestion}", increaseVersionAction.GetType().Name, increaseVersionAction.BuildMessage());
                await increaseVersionAction.TakeActionAsync();
            }
        }
    }

    private static NugetVersion Increase(string versionInProject)
    {
        var parsedVersion = new NugetVersion(versionInProject);
        if (parsedVersion.PrimaryVersion == null)
        {
            throw new InvalidOperationException($"Invalid version format: {versionInProject}");
        }
        var addedVersion = new Version(
            major: parsedVersion.PrimaryVersion.Major,
            minor: parsedVersion.PrimaryVersion.Minor,
            build: parsedVersion.PrimaryVersion.Build + 1);
        var increasedVersion = new NugetVersion($"{addedVersion}-{parsedVersion.AdditionalText}".TrimEnd('-'));
        return increasedVersion;
    }

    private static string SecurityOverrideKey(string projectPath, string packageName) =>
        $"{Path.GetFullPath(projectPath)}\0{packageName}";

    private bool HasActionTaken(Project project, List<IAction> allActions)
    {
        return 
            allActions.Any(a => a.SourceProject?.PathOnDisk == project.PathOnDisk && a.IsModifyingAction) || 
            project.ProjectReferences.Any(projectReference => HasActionTaken(projectReference, allActions));
    }
}
