using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.DuplicatePropertyPlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.DuplicatePropertyPlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<DuplicatePropertyDetector>();
    }
}