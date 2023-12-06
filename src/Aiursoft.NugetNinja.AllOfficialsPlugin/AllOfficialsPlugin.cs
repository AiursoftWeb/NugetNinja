using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class AllOfficialsPlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new AllOfficialsHandler() };
    }
}