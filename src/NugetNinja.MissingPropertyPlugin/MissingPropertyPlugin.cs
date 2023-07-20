using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyPlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new MissingPropertyHandler() };
    }
}