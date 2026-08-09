using Aiursoft.Canon;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class NugetServiceTests
{
    private NugetService _nugetService = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Initialize()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddHttpClient();
        services.AddLogging();
        services.Configure<AppSettings>(_ => { });
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<TransitiveSecurityOverrideService>();

        _serviceProvider = services.BuildServiceProvider();
        _nugetService = _serviceProvider.GetRequiredService<NugetService>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _serviceProvider.Dispose();
    }

    [TestMethod]
    public async Task TestGetLatestVersion()
    {
        // Newtonsoft.Json is a very common package.
        var version = await _nugetService.GetLatestVersion("Newtonsoft.Json", new[] { "net6.0" });
        Assert.IsNotNull(version.PrimaryVersion);
        Console.WriteLine($"Latest version of Newtonsoft.Json: {version}");
    }
    
    [TestMethod]
    public async Task TestGetApiEndpoint()
    {
        var endpoint = await _nugetService.GetApiEndpoint();
        Assert.IsNotNull(endpoint.PackageBaseAddress);
        Assert.IsNotNull(endpoint.RegistrationsBaseUrl);
        Console.WriteLine($"PackageBaseAddress: {endpoint.PackageBaseAddress}");
        Console.WriteLine($"RegistrationsBaseUrl: {endpoint.RegistrationsBaseUrl}");
    }

    [TestMethod]
    public async Task TestGetPackageDeprecationInfo()
    {
        // Aiursoft.Scanner is a package that might have deprecation info or at least we can test it.
        var package = new Package("Aiursoft.Scanner", new NugetVersion("3.1.1.2"));
        var info = await _nugetService.GetPackageDeprecationInfo(package);
        Assert.IsNotNull(info);
        // Even if not deprecated, the response should be valid.
    }

    [TestMethod]
    public async Task TestGetKnownVulnerabilitiesUsesCurrentNugetAuditPages()
    {
        var vulnerable = await _nugetService.GetKnownVulnerabilities(
            new Package("SQLitePCLRaw.lib.e_sqlite3", new NugetVersion("2.1.11")));
        var fixedVersion = await _nugetService.GetKnownVulnerabilities(
            new Package("SQLitePCLRaw.lib.e_sqlite3", new NugetVersion("2.1.12")));

        Assert.IsTrue(vulnerable.Any(advisory =>
                advisory.AdvisoryUrl.Contains("GHSA-2m69-gcr7-jv3q", StringComparison.OrdinalIgnoreCase)),
            "The current NuGet vulnerability pages should flag SQLitePCLRaw.lib.e_sqlite3 2.1.11.");
        Assert.IsFalse(fixedVersion.Any(advisory =>
                advisory.AdvisoryUrl.Contains("GHSA-2m69-gcr7-jv3q", StringComparison.OrdinalIgnoreCase)),
            "SQLitePCLRaw.lib.e_sqlite3 2.1.12 should be outside the affected range.");
    }

    [TestMethod]
    public async Task TestSqlite3ReplacementDependencyTreeCanBeSafelyAudited()
    {
        var scanner = _serviceProvider.GetRequiredService<TransitiveSecurityOverrideService>();

        var vulnerabilities = await scanner.GetKnownVulnerabilityIdsInClosureAsync(
            new Package("SQLitePCLRaw.bundle_e_sqlite3", new NugetVersion("3.0.5")));

        Assert.AreEqual(0, vulnerabilities.Count,
            "The real SQLitePCLRaw 3.x replacement tree must be completely auditable and vulnerability-free before NugetNinja can retire the 2.x override automatically.");
    }
}
