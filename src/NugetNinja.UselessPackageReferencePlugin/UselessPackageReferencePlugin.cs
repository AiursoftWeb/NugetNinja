

using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class UselessPackageReferencePlugin : INinjaPlugin
{
    public CommandHandler[] Install() => new CommandHandler[] { new PackageReferenceHandler() };
}
