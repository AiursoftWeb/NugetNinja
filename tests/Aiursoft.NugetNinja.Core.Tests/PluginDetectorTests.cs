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
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;
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

    [TestMethod]
    public async Task UselessPackageReferenceDetector_RemovesOverrideWhenParentMovesToNextMajor()
    {
        var project = await CreateTempProjectAsync("ParentMajorUpgrade.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.11")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.12"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateUselessPackageDetectorServices("3.0.5");
        var detector = services.GetRequiredService<UselessPackageReferenceDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsTrue(actions.OfType<UselessPackageReference>().Any(action =>
                action.TargetPackage.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "The parent owns the major upgrade, so the old security override must be retired.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task UselessPackageReferenceDetector_FailsClosedWhenRetirementCannotBeAudited()
    {
        var project = await CreateTempProjectAsync("UnsafeRetirement.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.11")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.12"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateUselessPackageDetectorServices(
            "3.0.5",
            failVulnerabilityFeed: true);
        var detector = services.GetRequiredService<UselessPackageReferenceDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsFalse(actions.OfType<UselessPackageReference>().Any(action =>
                action.TargetPackage.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "The override must remain when its replacement dependency tree cannot be audited.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task UselessPackageReferenceDetector_KeepsOverrideWhenHigherReplacementIsVulnerable()
    {
        var project = await CreateTempProjectAsync("VulnerableReplacement.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.11")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.12"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateUselessPackageDetectorServices(
            "3.0.5",
            vulnerableReplacement: true);
        var detector = services.GetRequiredService<UselessPackageReferenceDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsFalse(actions.OfType<UselessPackageReference>().Any(action =>
                action.TargetPackage.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "A higher version is not a safe replacement when its dependency tree is vulnerable.");

        Cleanup(project.PathOnDisk);
    }

    // ── TransitiveSecurityOverrideService ─────────────────────────

    [TestMethod]
    public async Task PackageReferenceUpgradeDetector_FreezesSecurityOverrideButUpgradesParent()
    {
        var project = await CreateTempProjectAsync("SecurityOverride.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.10")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.12"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateSecurityOverrideServices();
        var detector = services.GetRequiredService<PackageReferenceUpgradeDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsTrue(actions.OfType<PossiblePackageUpgrade>().Any(action =>
                action.Package.Name == "Microsoft.EntityFrameworkCore.Sqlite" &&
                action.NewVersion == new NugetVersion("10.0.11")),
            "The parent package should still be upgraded.");
        Assert.IsFalse(actions.OfType<PossiblePackageUpgrade>().Any(action =>
                action.Package.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "A safe transitive override must not chase the latest major version.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task PackageReferenceUpgradeDetector_FailsClosedWhenVulnerabilityFeedIsUnavailable()
    {
        var project = await CreateTempProjectAsync("UnavailableAudit.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.10")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.12"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateSecurityOverrideServices(failVulnerabilityFeed: true);
        var detector = services.GetRequiredService<PackageReferenceUpgradeDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsFalse(actions.OfType<PossiblePackageUpgrade>().Any(action =>
                action.Package.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "An unavailable vulnerability feed must freeze a possible security override.");

        Cleanup(project.PathOnDisk);
    }

    [TestMethod]
    public async Task PackageReferenceUpgradeDetector_DoesNotFreezeAVulnerableOverride()
    {
        var project = await CreateTempProjectAsync("MultipleAdvisories.csproj");
        project.PackageReferences.AddRange([
            new Package("Microsoft.EntityFrameworkCore.Sqlite", new NugetVersion("10.0.10")),
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("2.1.12"))
        ]);
        var context = new WorkspaceModel { AllProjects = [project] };

        using var services = CreateSecurityOverrideServices(includePersistentVulnerability: true);
        var detector = services.GetRequiredService<PackageReferenceUpgradeDetector>();
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(context))
        {
            actions.Add(action);
        }

        Assert.IsTrue(actions.OfType<PossiblePackageUpgrade>().Any(action =>
                action.Package.Name == "SQLitePCLRaw.bundle_e_sqlite3"),
            "A direct reference that still has a known vulnerability is not a safe TSO and must not be frozen.");

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

    private static ServiceProvider CreateUselessPackageDetectorServices(
        string transitiveSqliteVersion,
        bool failVulnerabilityFeed = false,
        bool vulnerableReplacement = false)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddLogging();
        services.Configure<AppSettings>(options =>
            options.CustomNugetServer = "https://packages.test/v3/index.json");
        services.AddSingleton(new HttpClient(new NugetDependencyHandler(
            transitiveSqliteVersion,
            failVulnerabilityFeed,
            vulnerableReplacement)));
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<TransitiveSecurityOverrideService>();
        services.AddTransient<UselessPackageReferenceDetector>();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateSecurityOverrideServices(
        bool failVulnerabilityFeed = false,
        bool includePersistentVulnerability = false)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddLogging();
        services.Configure<AppSettings>(options =>
            options.CustomNugetServer = "https://packages.test/v3/index.json");
        services.AddSingleton(new HttpClient(new SecurityOverrideHandler(
            failVulnerabilityFeed,
            includePersistentVulnerability)));
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<TransitiveSecurityOverrideService>();
        services.AddTransient<PackageReferenceUpgradeDetector>();
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

    private sealed class NugetDependencyHandler(
        string transitiveSqliteVersion,
        bool failVulnerabilityFeed,
        bool vulnerableReplacement) : HttpMessageHandler
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

            if (uri.AbsolutePath == "/v3/vulnerabilities/index.json")
            {
                if (failVulnerabilityFeed)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }

                return Task.FromResult(CreateResponse("""
                    [
                      {
                        "@name": "base",
                        "@id": "https://api.nuget.org/v3-vulnerabilities/empty.json"
                      }
                    ]
                    """, "application/json"));
            }

            if (uri.AbsolutePath == "/v3-vulnerabilities/empty.json")
            {
                var vulnerabilities = vulnerableReplacement
                    ? """
                      {
                        "sqlitepclraw.bundle_e_sqlite3": [
                          {
                            "url": "https://github.com/advisories/GHSA-replacement",
                            "severity": 2,
                            "versions": "[3.0.5]"
                          }
                        ]
                      }
                      """
                    : "{}";
                return Task.FromResult(CreateResponse(vulnerabilities, "application/json"));
            }

            var dependencies = uri.AbsolutePath.Contains("microsoft.entityframeworkcore.sqlite/",
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

    private sealed class SecurityOverrideHandler(
        bool failVulnerabilityFeed,
        bool includePersistentVulnerability) : HttpMessageHandler
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

            if (uri.AbsolutePath == "/v3/vulnerabilities/index.json")
            {
                if (failVulnerabilityFeed)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }

                return Task.FromResult(CreateResponse("""
                    [
                      {
                        "@name": "base",
                        "@id": "https://api.nuget.org/v3-vulnerabilities/base.json"
                      }
                    ]
                    """, "application/json"));
            }

            if (uri.AbsolutePath == "/v3-vulnerabilities/base.json")
            {
                var vulnerabilities = includePersistentVulnerability
                    ? """
                      {
                        "sqlitepclraw.bundle_e_sqlite3": [
                          {
                            "url": "https://github.com/advisories/GHSA-unrelated",
                            "severity": 2,
                            "versions": "(, 2.1.12]"
                          }
                        ],
                        "sqlitepclraw.lib.e_sqlite3": [
                          {
                            "url": "https://github.com/advisories/GHSA-test",
                            "severity": 2,
                            "versions": "(, 2.1.11]"
                          }
                        ]
                      }
                      """
                    : """
                      {
                        "sqlitepclraw.lib.e_sqlite3": [
                          {
                            "url": "https://github.com/advisories/GHSA-test",
                            "severity": 2,
                            "versions": "(, 2.1.11]"
                          }
                        ]
                      }
                      """;
                return Task.FromResult(CreateResponse(vulnerabilities, "application/json"));
            }

            if (uri.AbsolutePath.EndsWith("/microsoft.entityframeworkcore.sqlite/index.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateResponse("""{"versions":["10.0.10","10.0.11"]}""",
                    "application/json"));
            }

            if (uri.AbsolutePath.EndsWith("/sqlitepclraw.bundle_e_sqlite3/index.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateResponse("""{"versions":["2.1.11","2.1.12","3.0.5"]}""",
                    "application/json"));
            }

            var dependencies = string.Empty;
            if (uri.AbsolutePath.Contains("microsoft.entityframeworkcore.sqlite/10.0.10",
                    StringComparison.OrdinalIgnoreCase))
            {
                dependencies = """<dependency id="SQLitePCLRaw.bundle_e_sqlite3" version="2.1.11" />""";
            }
            else if (uri.AbsolutePath.Contains("sqlitepclraw.bundle_e_sqlite3/2.1.11",
                         StringComparison.OrdinalIgnoreCase))
            {
                dependencies = """<dependency id="SQLitePCLRaw.lib.e_sqlite3" version="2.1.11" />""";
            }
            else if (uri.AbsolutePath.Contains("sqlitepclraw.bundle_e_sqlite3/2.1.12",
                         StringComparison.OrdinalIgnoreCase))
            {
                dependencies = """<dependency id="SQLitePCLRaw.lib.e_sqlite3" version="2.1.12" />""";
            }

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
