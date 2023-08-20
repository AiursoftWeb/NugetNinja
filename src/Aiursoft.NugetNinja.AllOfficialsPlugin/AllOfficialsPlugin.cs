using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class AllOfficialsPlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new AllOfficialsHandler() };
    }
}