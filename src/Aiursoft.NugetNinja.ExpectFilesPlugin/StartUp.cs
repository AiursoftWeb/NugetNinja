using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient("NugetNinja.ExpectedFiles.Authenticated")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        services.AddTransient(provider => new ExpectFilesDetector(
            provider.GetRequiredService<HttpClient>(),
            provider.GetRequiredService<ILogger<ExpectFilesDetector>>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("NugetNinja.ExpectedFiles.Authenticated")));
    }
}
