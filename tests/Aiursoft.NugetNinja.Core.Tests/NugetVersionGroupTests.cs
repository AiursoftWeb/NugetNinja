using System.Net;
using System.Text;
using Aiursoft.Canon;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class NugetVersionGroupTests
{
    [TestMethod]
    [DataRow(null, "1.0.0", "1.0.0", "1.0.0", "1.0.0")]
    [DataRow(null, "2.0.0|4.0.0", "1.0.0", "2.0.0", "2.0.0")]
    [DataRow(null, "offline", "1.0.0", "1.0.0", "1.0.0")]
    [DataRow(null, "2.0.0|3.0.0", "3.0.0", "3.0.0", "3.0.0")]
    [DataRow(null, "1.0.0", "2.0.0", "2.0.0", "1.0.0")]
    [DataRow("conditional", "2.0.0|3.0.0", "1.0.0", "1.0.0", "1.0.0")]
    [DataRow("property", "2.0.0|3.0.0", "1.0.0", "1.0.0", "$(NativeVersion)")]
    [DataRow("single", "2.0.0|3.0.0", "1.0.0", "3.0.0", null)]
    [DataRow("frozen", "2.0.0|3.0.0", "1.0.0", "1.0.0", "1.0.0")]
    public async Task UpgradeOnly_RespectsVersionGroups(string? declaration, string nativeVersions,
        string originalManaged, string expectedManaged, string? expectedNative)
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "Sample.csproj");
            var nativeReference = declaration switch
            {
                "single" => "",
                "conditional" => """<PackageReference Include="Sample.Native" Version="1.0.0" Condition="'$(OS)' != ''" />""",
                "property" => """<PackageReference Include="Sample.Native" Version="$(NativeVersion)" />""",
                _ => """<PackageReference Include="Sample.Native" Version="1.0.0" />"""
            };
            await File.WriteAllTextAsync(path, $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Sample.Managed" Version="{{originalManaged}}" />
                    {{nativeReference}}
                    <PackageReference Include="Sample.Independent" Version="1.0.0" />
                  </ItemGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "ninja.yaml"), """
                nuget:
                  sameVersionGroups:
                    sample:
                      - sample.managed
                      - Sample.Native
                """);
            using var services = CreateServices(nativeVersions, declaration == "frozen");
            var runner = services.GetRequiredService<RunAllOfficialPluginsService>();
            await runner.RunAllPlugins(directory.FullName, shouldTakeAction: true, onlyRunUpdatePlugin: true);
            var updated = await File.ReadAllTextAsync(path);
            StringAssert.Contains(updated, $"Include=\"Sample.Managed\" Version=\"{expectedManaged}\"");
            StringAssert.Contains(updated, "Include=\"Sample.Independent\" Version=\"2.0.0\"");
            if (expectedNative != null)
                StringAssert.Contains(updated, $"Include=\"Sample.Native\" Version=\"{expectedNative}\"");
            else
                Assert.IsFalse(updated.Contains("Sample.Native", StringComparison.Ordinal));

            await runner.RunAllPlugins(directory.FullName, shouldTakeAction: true, onlyRunUpdatePlugin: true);
            Assert.AreEqual(updated, await File.ReadAllTextAsync(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task OverlappingGroupsAreRejectedBeforeProjectParsing()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "ninja.yaml"), """
                nuget:
                  sameVersionGroups:
                    first: [Sample.A, Sample.B]
                    second: [sample.b, Sample.C]
                """);
            using var services = CreateServices("1.0.0", false);
            var extractor = services.GetRequiredService<Extractor>();
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => extractor.Parse(directory.FullName));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task GroupWritePreservesFileIfAReferenceChangesBeforeApplication()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "Sample.csproj");
            const string original = """
                <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
                  <PackageReference Include="Sample.Managed" Version="1.0.0" />
                  <PackageReference Include="Sample.Native" Version="1.0.0" />
                </ItemGroup></Project>
                """;
            await File.WriteAllTextAsync(path, original);
            using var services = CreateServices("2.0.0", false);
            var extractor = services.GetRequiredService<Extractor>();
            var workspace = await extractor.Parse(directory.FullName);
            const string changed = """
                <Project Sdk="Microsoft.NET.Sdk"><ItemGroup>
                  <PackageReference Include="Sample.Managed" Version="1.0.0" />
                </ItemGroup></Project>
                """;
            await File.WriteAllTextAsync(path, changed);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => workspace.AllProjects.Single()
                .SetPackageGroupVersionAsync(["Sample.Managed", "Sample.Native"],
                    new NugetVersion("2.0.0")));
            Assert.AreEqual(changed, await File.ReadAllTextAsync(path));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static ServiceProvider CreateServices(string nativeVersions, bool freeze)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddLogging();
        services.Configure<AppSettings>(options => options.CustomNugetServer = "https://packages.test/v3/index.json");
        services.AddSingleton(new HttpClient(new GroupFeed(nativeVersions, freeze)));
        services.AddTransient<Extractor>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        new AllOfficialsPlugin.StartUp().ConfigureServices(services);
        services.AddTransient<RunAllOfficialPluginsService>();
        return services.BuildServiceProvider();
    }

    private sealed class GroupFeed(string nativeVersions, bool freeze) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var nativeIndex = path == "/packages/sample.native/index.json";
            var independentIndex = path == "/packages/sample.independent/index.json";
            if (nativeIndex && nativeVersions == "offline")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var body = path switch
            {
                "/v3/index.json" => """
                    {"resources":[
                      {"@id":"https://packages.test/packages/","@type":"PackageBaseAddress/3.0.0"},
                      {"@id":"https://packages.test/registrations/","@type":"RegistrationsBaseUrl/3.6.0"},
                      {"@id":"https://packages.test/audit/index.json","@type":"VulnerabilityInfo/6.7.0"}
                    ]}
                    """,
                "/audit/index.json" => """[{"@name":"base","@id":"https://packages.test/audit/base.json"}]""",
                "/audit/base.json" when freeze => """
                    {"sample.native":[{"url":"https://example.test/advisory","severity":2,"versions":"[0.9.0]"}]}
                    """,
                "/audit/base.json" => "{}",
                _ when nativeIndex => System.Text.Json.JsonSerializer.Serialize(new
                {
                    versions = new[] { "1.0.0" }.Concat(nativeVersions.Split('|')).Distinct()
                }),
                _ when independentIndex => """{"versions":["1.0.0","2.0.0"]}""",
                "/packages/sample.managed/index.json" => """{"versions":["1.0.0","2.0.0","3.0.0"]}""",
                _ when freeze && path.StartsWith("/packages/sample.managed/", StringComparison.Ordinal) =>
                    """<package><metadata><dependencies><dependency id="Sample.Native" version="0.9.0" /></dependencies></metadata></package>""",
                _ => "<package><metadata><dependencies /></metadata></package>"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8)
            });
        }
    }
}
