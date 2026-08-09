using System.Net;
using System.Text;
using Aiursoft.Canon;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class TransitiveSecurityOverrideWorkflowTests
{
    [TestMethod]
    [DataRow("2.1.11", true, DisplayName = "Parent still vulnerable - keep the override")]
    [DataRow("2.1.12", false, DisplayName = "Parent adopts the safe floor - retire the override")]
    [DataRow("3.0.5", false, DisplayName = "Parent adopts the next major - let the parent own it")]
    public async Task AllOfficialPlugins_ManageSecurityOverrideLifecycle(
        string upgradedParentDependency,
        bool expectDirectOverride)
    {
        await AssertWorkflowResult(
            upgradedParentDependency,
            expectDirectOverride,
            onlyRunUpdatePlugin: false);
    }

    [TestMethod]
    public async Task UpdateOnlyMode_StillRetiresSatisfiedSecurityOverride()
    {
        await AssertWorkflowResult(
            "2.1.12",
            expectDirectOverride: false,
            onlyRunUpdatePlugin: true,
            includeUnrelatedRedundantReference: true);
    }

    private static async Task AssertWorkflowResult(
        string upgradedParentDependency,
        bool expectDirectOverride,
        bool onlyRunUpdatePlugin,
        bool includeUnrelatedRedundantReference = false)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "TestProject.csproj");
        var unrelatedReferences = includeUnrelatedRedundantReference
            ? """
                  <PackageReference Include="Unrelated.Parent" Version="1.0.0" />
                  <PackageReference Include="Unrelated.Child" Version="1.0.0" />
              """
            : string.Empty;
        await File.WriteAllTextAsync(projectPath, $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>TestProject</AssemblyName>
                <RootNamespace>TestProject</RootNamespace>
                <IsTestProject>false</IsTestProject>
                <IsPackable>false</IsPackable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.10" />
                <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="2.1.12" />
            {{unrelatedReferences}}
              </ItemGroup>
            </Project>
            """);

        try
        {
            using var services = CreateServices(upgradedParentDependency);
            var runner = services.GetRequiredService<RunAllOfficialPluginsService>();

            await runner.RunAllPlugins(directory, shouldTakeAction: true, onlyRunUpdatePlugin);

            var result = await File.ReadAllTextAsync(projectPath);
            StringAssert.Contains(result,
                "Include=\"Microsoft.EntityFrameworkCore.Sqlite\" Version=\"10.0.11\"",
                "The parent package should be upgraded first.");
            Assert.AreEqual(
                expectDirectOverride,
                result.Contains("Include=\"SQLitePCLRaw.bundle_e_sqlite3\"", StringComparison.Ordinal),
                expectDirectOverride
                    ? "The direct security floor must remain while the parent is still vulnerable."
                    : "The direct security override should be retired after the parent supplies a safe dependency.");
            if (includeUnrelatedRedundantReference)
            {
                StringAssert.Contains(result,
                    "Include=\"Unrelated.Child\" Version=\"1.0.0\"",
                    "Update-only mode must not remove ordinary redundant references while retiring a recorded TSO.");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ServiceProvider CreateServices(string upgradedParentDependency)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddLogging();
        services.Configure<AppSettings>(options =>
            options.CustomNugetServer = "https://packages.test/v3/index.json");
        services.AddSingleton(new HttpClient(new WorkflowFeedHandler(upgradedParentDependency)));
        services.AddTransient<Extractor>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        new AllOfficialsPlugin.StartUp().ConfigureServices(services);
        services.AddTransient<RunAllOfficialPluginsService>();
        return services.BuildServiceProvider();
    }

    private sealed class WorkflowFeedHandler(string upgradedParentDependency) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("A request URI is required.");
            if (uri.AbsolutePath == "/v3/index.json")
            {
                return Task.FromResult(Json("""
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
                    """));
            }

            if (uri.AbsolutePath == "/v3/vulnerabilities/index.json")
            {
                return Task.FromResult(Json("""
                    [
                      {
                        "@name": "base",
                        "@id": "https://api.nuget.org/v3-vulnerabilities/base.json"
                      }
                    ]
                    """));
            }

            if (uri.AbsolutePath == "/v3-vulnerabilities/base.json")
            {
                return Task.FromResult(Json("""
                    {
                      "sqlitepclraw.lib.e_sqlite3": [
                        {
                          "url": "https://github.com/advisories/GHSA-test",
                          "severity": 2,
                          "versions": "(, 2.1.11]"
                        }
                      ]
                    }
                    """));
            }

            if (uri.AbsolutePath.EndsWith("/microsoft.entityframeworkcore.sqlite/index.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"versions":["10.0.10","10.0.11"]}"""));
            }

            if (uri.AbsolutePath.EndsWith("/sqlitepclraw.bundle_e_sqlite3/index.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"versions":["2.1.11","2.1.12","3.0.5"]}"""));
            }

            if (uri.AbsolutePath == "/query")
            {
                return Task.FromResult(Json("""{"data":[]}"""));
            }

            var dependencies = GetDependencies(uri.AbsolutePath);
            return Task.FromResult(Xml($"""
                <?xml version="1.0" encoding="utf-8"?>
                <package>
                  <metadata>
                    <dependencies>{dependencies}</dependencies>
                  </metadata>
                </package>
                """));
        }

        private string GetDependencies(string path)
        {
            if (path.Contains("microsoft.entityframeworkcore.sqlite/10.0.10",
                    StringComparison.OrdinalIgnoreCase))
            {
                return """<dependency id="SQLitePCLRaw.bundle_e_sqlite3" version="2.1.11" />""";
            }

            if (path.Contains("microsoft.entityframeworkcore.sqlite/10.0.11",
                    StringComparison.OrdinalIgnoreCase))
            {
                return $"""<dependency id="SQLitePCLRaw.bundle_e_sqlite3" version="{upgradedParentDependency}" />""";
            }

            if (path.Contains("sqlitepclraw.bundle_e_sqlite3/2.1.11",
                    StringComparison.OrdinalIgnoreCase))
            {
                return """<dependency id="SQLitePCLRaw.lib.e_sqlite3" version="2.1.11" />""";
            }

            if (path.Contains("sqlitepclraw.bundle_e_sqlite3/2.1.12",
                    StringComparison.OrdinalIgnoreCase))
            {
                return """<dependency id="SQLitePCLRaw.lib.e_sqlite3" version="2.1.12" />""";
            }

            if (path.Contains("sqlitepclraw.bundle_e_sqlite3/3.0.5",
                    StringComparison.OrdinalIgnoreCase))
            {
                return """<dependency id="SQLite" version="3.53.4" />""";
            }

            if (path.Contains("unrelated.parent/1.0.0",
                    StringComparison.OrdinalIgnoreCase))
            {
                return """<dependency id="Unrelated.Child" version="1.0.0" />""";
            }

            return string.Empty;
        }

        private static HttpResponseMessage Json(string content) =>
            CreateResponse(content, "application/json");

        private static HttpResponseMessage Xml(string content) =>
            CreateResponse(content, "application/xml");

        private static HttpResponseMessage CreateResponse(string content, string mediaType)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, mediaType)
            };
        }
    }
}
