using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.MissingPropertyPlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<ProjectTypeDetector>();
        services.AddTransient<MissingPropertyDetector>();
    }
}