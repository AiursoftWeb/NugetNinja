

using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class DeprecatedPackagePlugin : INinjaPlugin
{
    public CommandHandler[] Install() => new CommandHandler[] { new DeprecatedPackageHandler() };
}
