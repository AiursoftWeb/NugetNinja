using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class DeprecatedPackagePlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new DeprecatedPackageHandler() };
    }
}