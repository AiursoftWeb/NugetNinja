

using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.NugetNinja.Core;

public interface IStartUp
{
    public void ConfigureServices(IServiceCollection services);
}
