using System.Net;
using System.Text;
using Aiursoft.Canon;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.Models;
using Aiursoft.NugetNinja.DuplicatePropertyPlugin.Services;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Models;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WorkspaceModel = Aiursoft.NugetNinja.Core.Model.Workspace.Model;

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

        var context = new WorkspaceModel { AllProjects = [project] };
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

        var context = new WorkspaceModel { AllProjects = [project] };
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

        var context = new WorkspaceModel { AllProjects = [project] };
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

    // ── UselessPackageReferenceDetector ────────────────────────────

    [TestMethod]
    public async Task UselessPackageReferenceDetector_KeepsReferenceWhenRemovalWouldDowngradePackage()
    {
        var project = await CreateTempProjectAsync("VersionPinned.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.10")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.12"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateUselessPackageDetectorServices("2.1.11");
        var detector = services.GetRequiredService<UselessPackageReferenceDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsFalse(actions.OfType<UselessPackageReference>().Any(action =>
                action.TargetPackage.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "The newer direct reference must remain because the transitive version would be lower.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task UselessPackageReferenceDetector_RemovesReferenceWhenTransitiveVersionIsNotLower()
    {
        var project = await CreateTempProjectAsync("RedundantReference.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.10")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.11"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateUselessPackageDetectorServices("2.1.11");
        var detector = services.GetRequiredService<UselessPackageReferenceDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsTrue(actions.OfType<UselessPackageReference>().Any(action =>
                action.TargetPackage.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "An equal transitive version makes the direct reference safely removable.");

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

    private static ServiceProvider CreateUselessPackageDetectorServices(string transitiveSqliteVersion)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddLogging();
        services.Configure<AppSettings>(options =>
            options.CustomNugetServer = "https://packages.test/v3/index.json");
        services.AddSingleton(new HttpClient(new NugetDependencyHandler(transitiveSqliteVersion)));
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<UselessPackageReferenceDetector>();
        return services.BuildServiceProvider();
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

    private sealed class NugetDependencyHandler(string transitiveSqliteVersion) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("A request URI is required.");
            if (uri.AbsolutePath == "/v3/index.json")
            {
                return Task.FromResult(CreateResponse("""
                    {
                      "resources": [
                        {
                          "@id": "https://packages.test/v3-flatcontainer/",
                          "@type": "PackageBaseAddress/3.0.0"
                        },
                        {
                          "@id": "https://packages.test/registrations/",
                          "@type": "RegistrationsBaseUrl/3.6.0"
                        }
                      ]
                    }
                    """, "application/json"));
            }

            var dependencies = uri.AbsolutePath.Contains("microsoft.entityframeworkcore.sqlite/10.0.10",
                StringComparison.OrdinalIgnoreCase)
                ? $"""<dependency id="SQLitePCLRaw.bundle_e_sqlite3" version="{transitiveSqliteVersion}" />"""
                : string.Empty;
            return Task.FromResult(CreateResponse($"""
                <?xml version="1.0" encoding="utf-8"?>
                <package>
                  <metadata>
                    <dependencies>{dependencies}</dependencies>
                  </metadata>
                </package>
                """, "application/xml"));
        }

        private static HttpResponseMessage CreateResponse(string content, string mediaType)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, mediaType)
            };
        }
    }
}
