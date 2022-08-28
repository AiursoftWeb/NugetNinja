using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class AllOfficialsPlugin : INinjaPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new AllOfficialsHandler() };
    }
}
