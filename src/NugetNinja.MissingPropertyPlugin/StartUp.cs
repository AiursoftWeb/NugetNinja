using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<MissingPropertyDetector>();
    }
}