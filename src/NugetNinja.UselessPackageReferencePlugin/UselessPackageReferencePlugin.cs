using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class UselessPackageReferencePlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new PackageReferenceHandler() };
    }
}