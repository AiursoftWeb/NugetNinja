using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class DeprecatedPackagePlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new DeprecatedPackageHandler() };
    }
}