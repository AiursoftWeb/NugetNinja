using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class PossiblePackageUpgradePlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new PackageUpgradeHandler() };
    }
}