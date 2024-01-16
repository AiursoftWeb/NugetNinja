using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<UselessProjectReferenceDetector>();
    }
}