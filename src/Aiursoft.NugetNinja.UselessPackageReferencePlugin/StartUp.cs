using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddTransient<TransitiveSecurityOverrideService>();
        services.AddTransient<UselessPackageReferenceDetector>();
    }
}
