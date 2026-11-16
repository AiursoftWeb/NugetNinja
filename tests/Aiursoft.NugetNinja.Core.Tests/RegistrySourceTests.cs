using System.Net;
using System.Text.Json;
using Aiursoft.Canon;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class RegistrySourceTests
{
    [TestMethod]
    public async Task VersionDependenciesDeprecationAndAuditUseTheSelectedRegistry()
    {
        using var handler = new InternalRegistryHandler();
        using var provider = Services(handler);
        var service = provider.GetRequiredService<NugetService>();
        var package = new Package("Example.Package", new NugetVersion("1.0.0"));
        Assert.AreEqual("1.1.0", (await service.GetLatestVersion(package.Name, ["net10.0"])).ToString());
        Assert.AreEqual("Example.Child", (await service.GetPackageDependencies(package)).Single().Name);
        var info = await service.GetPackageDeprecationInfo(package);
        Assert.AreEqual("Use the replacement.", info.Deprecation?.Message);
        Assert.AreEqual("https://advisories.test/known", (await service.GetKnownVulnerabilities(package)).Single().AdvisoryUrl);
        Assert.HasCount(0, await service.GetKnownVulnerabilities(new Package(package.Name, new NugetVersion("1.1.0"))));
        Assert.IsTrue(handler.Requests.Any(uri => uri.AbsolutePath == "/audit/snapshot.json"));
        Assert.IsTrue(handler.Requests.All(uri => uri.Host == "registry.test"));
    }

    [TestMethod]
    public async Task ARegistryWithoutAuditDataDoesNotFallBackToPublicNuget()
    {
        using var handler = new InternalRegistryHandler { HasAudit = false };
        using var provider = Services(handler);
        var service = provider.GetRequiredService<NugetService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetKnownVulnerabilities(
            new Package("Example.Package", new NugetVersion("1.0.0"))));
        Assert.IsTrue(handler.Requests.All(uri => uri.Host == "registry.test"));
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task AnAuditOutageFailsClosedWithoutPublicFallback()
    {
        using var handler = new InternalRegistryHandler { AuditUnavailable = true };
        using var provider = Services(handler);
        await Assert.ThrowsAsync<WebException>(() => provider.GetRequiredService<NugetService>()
            .GetKnownVulnerabilities(new Package("Example.Package", new NugetVersion("1.0.0"))));
        Assert.IsTrue(handler.Requests.All(uri => uri.Host == "registry.test"));
    }

    [TestMethod]
    public async Task AnEmptyVulnerabilityIndexIsNotReportedAsSafe()
    {
        using var handler = new InternalRegistryHandler { EmptyAuditIndex = true };
        using var provider = Services(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() => provider.GetRequiredService<NugetService>()
            .GetKnownVulnerabilities(new Package("Example.Package", new NugetVersion("1.0.0"))));
    }

    [TestMethod]
    public async Task AStandardEmptyVulnerabilityPageIsSupported()
    {
        using var handler = new InternalRegistryHandler { EmptyAuditPage = true };
        using var provider = Services(handler);
        Assert.HasCount(0, await provider.GetRequiredService<NugetService>()
            .GetKnownVulnerabilities(new Package("Example.Package", new NugetVersion("1.0.0"))));
    }

    private static ServiceProvider Services(InternalRegistryHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.Configure<AppSettings>(options => options.CustomNugetServer = "https://registry.test/v3/index.json");
        services.AddSingleton(new HttpClient(handler));
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        return services.BuildServiceProvider();
    }

    private sealed class InternalRegistryHandler : HttpMessageHandler
    {
        public bool HasAudit { get; init; } = true;
        public bool AuditUnavailable { get; init; }
        public bool EmptyAuditIndex { get; init; }
        public bool EmptyAuditPage { get; init; }
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri);
            Assert.AreEqual("registry.test", uri.Host, "Registry queries must not escape to a public service.");
            if (uri.AbsolutePath == "/audit/index.json" && AuditUnavailable)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var body = uri.AbsolutePath switch
            {
                "/v3/index.json" => JsonSerializer.Serialize(new
                {
                    resources = new[]
                    {
                        new { id = "https://registry.test/flat/", type = "PackageBaseAddress/3.0.0" },
                        new { id = "https://registry.test/registrations/", type = "RegistrationsBaseUrl/3.6.0" },
                        new { id = "https://registry.test/search", type = "SearchQueryService/3.5.0" },
                        new { id = "https://registry.test/audit/index.json", type = "VulnerabilityInfo/6.7.0" }
                    }.Where(item => HasAudit || !item.type.StartsWith("VulnerabilityInfo", StringComparison.Ordinal))
                     .Select(item => new Dictionary<string, string> { ["@id"] = item.id, ["@type"] = item.type })
                }),
                "/flat/example.package/index.json" => """{"versions":["1.0.0","1.1.0"]}""",
                "/flat/example.package/1.0.0/example.package.nuspec" => """<package><metadata><dependencies><dependency id="Example.Child" version="1.0.0"/></dependencies></metadata></package>""",
                "/search" => """{"data":[{"id":"Example.Package","deprecation":{"message":"Use the replacement."}}]}""",
                "/audit/index.json" => EmptyAuditIndex ? "[]" : """[{"@name":"snapshot","@id":"https://registry.test/audit/snapshot.json"}]""",
                "/audit/snapshot.json" => EmptyAuditPage ? "[]" : """{"example.package":[{"url":"https://advisories.test/known","severity":1,"versions":"[1.0.0]"}]}""",
                _ => throw new InvalidOperationException("Unexpected registry request: " + uri)
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
