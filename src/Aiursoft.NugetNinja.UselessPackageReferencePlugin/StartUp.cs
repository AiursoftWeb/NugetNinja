using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<UselessPackageReferenceDetector>();
    }
}