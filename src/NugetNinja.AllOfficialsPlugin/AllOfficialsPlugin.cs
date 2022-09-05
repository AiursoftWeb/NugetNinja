using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class AllOfficialsPlugin : INinjaPlugin
{
    public CommandHandler[] Install() => new CommandHandler[] { new AllOfficialsHandler() };
}
