using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<PackageReferenceUpgradeDetector>();
    }
}