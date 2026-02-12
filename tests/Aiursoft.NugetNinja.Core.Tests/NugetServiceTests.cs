using Aiursoft.Canon;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class NugetServiceTests
{
    private NugetService _nugetService = null!;
    private HttpClient _httpClient = null!;
    private ServiceProvider _serviceProvider = null!;

    [TestInitialize]
    public void Initialize()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddHttpClient();
        services.AddLogging();
        services.Configure<AppSettings>(options => { });
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _nugetService = _serviceProvider.GetRequiredService<NugetService>();
        _httpClient = _serviceProvider.GetRequiredService<HttpClient>();
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
}
