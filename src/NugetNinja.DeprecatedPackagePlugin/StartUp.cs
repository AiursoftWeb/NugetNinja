using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<DeprecatedPackageDetector>();
    }
}