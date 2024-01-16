using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<DeprecatedPackageDetector>();
    }
}