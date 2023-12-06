using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class UselessPackageReferencePlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new PackageReferenceHandler() };
    }
}