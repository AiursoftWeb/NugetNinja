

using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class DeprecatedPackagePlugin : INinjaPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new DeprecatedPackageHandler() };
    }
}
