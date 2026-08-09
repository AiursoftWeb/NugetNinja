using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddTransient<TransitiveSecurityOverrideService>();
        services.AddTransient<PackageReferenceUpgradeDetector>();
    }
}
