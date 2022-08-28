

using Microsoft.Extensions.DependencyInjection;
using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<PackageReferenceUpgradeDetector>();
    }
}
