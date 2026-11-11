using System.Net;
using System.Text.Json;
using Aiursoft.Canon;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class RuntimeAlignedPackageUpgradeTests
{
    [TestMethod]
    [DataRow("net10.0", false, "10.0.14")]
    [DataRow("net11.0", false, "11.0.0")]
    [DataRow("net10.0", true, "11.0.0")]
    public async Task RuntimeReleaseKeepsPackageOnSelectedFrameworkUnlessCrossIsEnabled(
        string framework, bool allowCross, string expectedVersion)
    {
        using var services = CreateServices(allowCross,
            ["10.0.10", "10.0.11", "10.0.12", "10.0.13", "10.0.14", "11.0.0"]);

        var latest = await services.GetRequiredService<NugetService>()
            .GetLatestVersion("Microsoft.AspNetCore.Identity.EntityFrameworkCore", [framework]);

        Assert.AreEqual(expectedVersion, latest.ToString());
    }

    [TestMethod]
    public async Task NonRuntimeVersionSequenceStillSelectsLatestVersion()
    {
        using var services = CreateServices(false, ["7.4.0", "8.2.0", "11.0.0"]);

        var latest = await services.GetRequiredService<NugetService>()
            .GetLatestVersion("Example.Library", ["net10.0"]);

        Assert.AreEqual("11.0.0", latest.ToString());
    }

    private static ServiceProvider CreateServices(bool allowCross, string[] versions)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddLogging();
        services.Configure<AppSettings>(options =>
        {
            options.CustomNugetServer = "https://packages.test/v3/index.json";
            options.AllowCross = allowCross;
        });
        services.AddSingleton(new HttpClient(new ReleaseFeedHandler(versions)));
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        return services.BuildServiceProvider();
    }

    private sealed class ReleaseFeedHandler(string[] versions) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.RequestUri!.AbsolutePath == "/v3/index.json"
                ? """
                  {"resources":[
                    {"@id":"https://packages.test/flat/","@type":"PackageBaseAddress/3.0.0"},
                    {"@id":"https://packages.test/registration/","@type":"RegistrationsBaseUrl/3.6.0"}
                  ]}
                  """
                : JsonSerializer.Serialize(new { versions });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
