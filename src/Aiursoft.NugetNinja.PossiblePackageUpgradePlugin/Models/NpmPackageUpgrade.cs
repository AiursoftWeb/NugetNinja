using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;

public class NpmPackageUpgrade(
    string directory,
    string originalManifest,
    string originalLock,
    string updatedManifest,
    string updatedLock,
    string summary,
    Project? sourceProject = null) : IAction
{
    public Project? SourceProject => sourceProject;

    public string BuildMessage() => summary;

    public async Task TakeActionAsync()
    {
        var manifestPath = Path.Combine(directory, "package.json");
        var lockPath = Path.Combine(directory, "package-lock.json");
        if (await File.ReadAllTextAsync(manifestPath) != originalManifest ||
            await File.ReadAllTextAsync(lockPath) != originalLock)
        {
            throw new IOException("The npm manifest or lockfile changed after analysis. Run Ninja again.");
        }

        await File.WriteAllTextAsync(manifestPath, updatedManifest);
        await File.WriteAllTextAsync(lockPath, updatedLock);
    }
}
