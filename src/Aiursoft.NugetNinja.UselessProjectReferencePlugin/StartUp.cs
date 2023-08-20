using Aiursoft.CommandFramework.Abstracts;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<UselessProjectReferenceDetector>();
    }
}