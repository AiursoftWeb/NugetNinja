

using Microsoft.Extensions.DependencyInjection;
using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<DeprecatedPackageDetector>();
    }
}
