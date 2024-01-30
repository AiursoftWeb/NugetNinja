using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<ExpectFilesDetector>();
    }
}
