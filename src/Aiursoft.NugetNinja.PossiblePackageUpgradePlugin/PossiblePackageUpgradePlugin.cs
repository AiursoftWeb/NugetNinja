using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class PossiblePackageUpgradePlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new PackageUpgradeHandler() };
    }
}