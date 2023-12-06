using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class UselessPackageReferencePlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new PackageReferenceHandler() };
    }
}