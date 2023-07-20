using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class PossiblePackageUpgradePlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new PackageUpgradeHandler() };
    }
}