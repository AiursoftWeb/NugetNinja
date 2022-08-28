using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class PossiblePackageUpgradePlugin : INinjaPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new PackageUpgradeHandler() };
    }
}
