using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class DeprecatedPackagePlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new DeprecatedPackageHandler() };
    }
}