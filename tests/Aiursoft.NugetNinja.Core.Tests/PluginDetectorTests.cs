using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.Models;
using Aiursoft.NugetNinja.DuplicatePropertyPlugin.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class PluginDetectorTests
{
    // ── DeprecatedPackageReplacement ────────────────────────────────

    [TestMethod]
    public async Task DeprecatedPackageReplacement_BuildMessage_ShowsAlternativeWhenAvailable()
    {
        var project = await CreateTempProjectAsync("Test.csproj");
        var replacement = new DeprecatedPackageReplacement(project,
            new Package("Old.Package", new NugetVersion("1.0.0")),
            new Package("New.Package", new NugetVersion("2.0.0")));

        var message = replacement.BuildMessage();

        Console.WriteLine($"BuildMessage: {message}");
        Assert.IsTrue(message.Contains("New.Package"),
            "Message should include the alternative package name.");
        Assert.IsTrue(message.Contains("Old.Package"),
            "Message should include the deprecated package name.");
        Assert.IsTrue(message.Contains("replace"),
            "Message should suggest replacement.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task DeprecatedPackageReplacement_BuildMessage_EmptyWhenNoAlternative()
    {
        var project = await CreateTempProjectAsync("Test.csproj");
        var replacement = new DeprecatedPackageReplacement(project,
            new Package("Old.Package", new NugetVersion("1.0.0")),
            null);

        var message = replacement.BuildMessage();

        Console.WriteLine($"BuildMessage: {message}");
        Assert.IsTrue(message.Contains("Old.Package"),
            "Message should mention the deprecated package.");
        Assert.IsFalse(message.Contains("Please consider to replace"),
            "Should NOT suggest replacement when no alternative exists.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task DeprecatedPackageReplacement_IsModifyingAction_OnlyWhenAlternativeExists()
    {
        var project = await CreateTempProjectAsync("Test.csproj");
        var withAlternative = new DeprecatedPackageReplacement(project,
            new Package("Old.Package", new NugetVersion("1.0.0")),
            new Package("New.Package", new NugetVersion("2.0.0")));
        var withoutAlternative = new DeprecatedPackageReplacement(project,
            new Package("Old.Package", new NugetVersion("1.0.0")),
            null);

        Assert.IsTrue(withAlternative.IsModifyingAction,
            "Should be modifying when alternative is available.");
        Assert.IsFalse(withoutAlternative.IsModifyingAction,
            "Should NOT be modifying when no alternative.");

        Cleanup(project.PathOnDisk);
    }

    // ── VulnerablePackageReplacement ────────────────────────────────

    [TestMethod]
    public async Task VulnerablePackageReplacement_IsNotModifying()
    {
        var project = await CreateTempProjectAsync("Test.csproj");
        var vulnerable = new VulnerablePackageReplacement(project,
            new Package("Vuln.Package", new NugetVersion("1.0.0")));

        Assert.IsFalse(vulnerable.IsModifyingAction,
            "VulnerablePackageReplacement should not be modifying (auto-fix not implemented).");

        var message = vulnerable.BuildMessage();
        Assert.IsTrue(message.Contains("vulnerabilities"),
            "Message should mention vulnerabilities.");

        Cleanup(project.PathOnDisk);
    }

    // ── DuplicatePropertyDetector ───────────────────────────────────

    [TestMethod]
    public async Task DuplicatePropertyDetector_DetectsDuplicates()
    {
        var project = await CreateTempProjectAsync("DupTest.csproj", @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <Nullable>disable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>");

        var context = new Model { AllProjects = [project] };
        var detector = new DuplicatePropertyDetector(new LoggerFactory().CreateLogger<DuplicatePropertyDetector>());

        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.AreEqual(1, actions.Count, "Should detect exactly one duplicate property.");
        var msg = actions[0].BuildMessage();
        // HtmlAgilityPack lowercases element names internally even with OptionOutputOriginalCase
        Assert.IsTrue(msg.Contains("nullable"), $"Message should mention the duplicate property. Got: {msg}");
        Assert.IsTrue(msg.Contains("duplicate"), $"Message should say 'duplicate'. Got: {msg}");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task DuplicatePropertyDetector_NoDuplicates_ReturnsEmpty()
    {
        var project = await CreateTempProjectAsync("CleanTest.csproj", @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>");

        var context = new Model { AllProjects = [project] };
        var detector = new DuplicatePropertyDetector(new LoggerFactory().CreateLogger<DuplicatePropertyDetector>());

        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.AreEqual(0, actions.Count, "Should detect zero duplicates for clean project.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task DuplicatePropertyDetector_DetectsInSecondPropertyGroup()
    {
        var project = await CreateTempProjectAsync("MultiGroup.csproj", @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <Version>2.0.0</Version>
  </PropertyGroup>
</Project>");

        var context = new Model { AllProjects = [project] };
        var detector = new DuplicatePropertyDetector(new LoggerFactory().CreateLogger<DuplicatePropertyDetector>());

        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.AreEqual(1, actions.Count, "Should detect duplicate in second PropertyGroup.");
        var msg = actions[0].BuildMessage();
        Assert.IsTrue(msg.Contains("version"), $"Message should mention the duplicate property. Got: {msg}");

        Cleanup(project.PathOnDisk);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static async Task<Project> CreateTempProjectAsync(string fileName, string? csprojContent = null)
    {
        csprojContent ??= @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>";

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(path, csprojContent);

        var doc = new HtmlDocument
        {
            OptionOutputOriginalCase = true,
            OptionAutoCloseOnEnd = true,
            OptionWriteEmptyNodes = true
        };
        doc.LoadHtml(csprojContent);
        return new Project(path, doc.DocumentNode);
    }

    private static void Cleanup(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var dir = Path.GetDirectoryName(path);
                File.Delete(path);
                if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
        catch (IOException) { }
    }
}
