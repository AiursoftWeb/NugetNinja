using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;
using WorkspaceModel = Aiursoft.NugetNinja.Core.Model.Workspace.Model;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class NpmUpdateTests
{
    [TestMethod]
    [DataRow("0.58.0-dev-20261103", true)]
    [DataRow("3.0.0-5020620260917001", true)]
    [DataRow("1.2.3-rc.0", true)]
    [DataRow("1.2.3-01alpha", true)]
    [DataRow("1.2.3", false)]
    [DataRow("^1.2.3-beta.1", false)]
    [DataRow("~1.2.3-beta.1", false)]
    [DataRow("1.2.3-01", false)]
    [DataRow("1.2.3-rc.01", false)]
    [DataRow("1.2.3-rc..1", false)]
    [DataRow("1.2.3-rc.1\n", false)]
    [DataRow("npm:other@1.2.3-beta.1", false)]
    [DataRow("file:../1.2.3-beta.1", false)]
    public void OnlyExactCanonicalPrereleasesCanBePreserved(string version, bool expected)
    {
        Assert.AreEqual(expected, NpmVersion.IsExactPrerelease(version));
    }

    [TestMethod]
    [DataRow("1.2.3", "1.2.4", true)]
    [DataRow("1.2.3", "1.9.0", true)]
    [DataRow("1.2.3", "2.0.0", false)]
    [DataRow("1.2.3", "1.2.2", false)]
    [DataRow("0.4.1", "0.4.9", true)]
    [DataRow("0.4.1", "0.5.0", false)]
    [DataRow("0.4.1", "1.0.0", false)]
    [DataRow("0.0.1", "0.0.2", true)]
    [DataRow("0.0.1", "0.1.0", false)]
    public void CompatibleUpdatesNeverCrossTheApprovedLine(string current, string candidate, bool allowed)
    {
        Assert.AreEqual(allowed, NpmVersion.Parse(current).Allows(NpmVersion.Parse(candidate)));
    }

    [TestMethod]
    [DataRow("1.2.3", ">=1.2.3 <2.0.0")]
    [DataRow("0.4.1", ">=0.4.1 <0.5.0")]
    [DataRow("0.0.1", ">=0.0.1 <0.1.0")]
    public void NpmReceivesAnExplicitLowerAndUpperBound(string version, string range)
    {
        Assert.AreEqual(range, NpmVersion.Parse(version).UpdateRange);
    }

    [TestMethod]
    [DataRow("latest")]
    [DataRow("1.*")]
    [DataRow("1.2.3-beta.1")]
    [DataRow("file:../local-package")]
    [DataRow("npm:other@1.2.3")]
    public void UnsupportedVersionsCannotSilentlyExpandTheUpdateRange(string version)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => NpmVersion.Parse(version));
    }

    [TestMethod]
    public async Task ExistingRepositoriesDoNotInvokeNpmWithoutOptIn()
    {
        var detector = new NpmPackageUpgradeDetector();
        var model = new WorkspaceModel { RootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) };
        Assert.AreEqual(0, (await detector.AnalyzeAsync(model).ToListAsync()).Count);
        model.NinjaConfig.Npm = new NinjaNpmConfig { UpdateDependencies = false };
        Assert.AreEqual(0, (await detector.AnalyzeAsync(model).ToListAsync()).Count);
    }
    [TestMethod]
    public async Task SharedOptInSkipsRepositoriesWithoutNpmFiles()
    {
        var directory = Directory.CreateTempSubdirectory("ninja-no-npm-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Example.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var model = new WorkspaceModel
            {
                RootPath = directory,
                NinjaConfig = new NinjaConfig { Npm = new NinjaNpmConfig { UpdateDependencies = true } }
            };
            Assert.HasCount(0, await new NpmPackageUpgradeDetector().AnalyzeAsync(model).ToListAsync());
            await File.WriteAllTextAsync(Path.Combine(directory, "package.json"), "{}");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
                await new NpmPackageUpgradeDetector().AnalyzeAsync(model).ToListAsync());
        }
        finally { Directory.Delete(directory, true); }
    }

}
