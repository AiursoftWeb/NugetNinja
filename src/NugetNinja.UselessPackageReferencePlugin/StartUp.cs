

using Microsoft.Extensions.DependencyInjection;
using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class StartUp : IStartUp
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<UselessPackageReferenceDetector>();
    }
}
