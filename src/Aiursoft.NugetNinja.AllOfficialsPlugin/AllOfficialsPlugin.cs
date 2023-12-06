using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class AllOfficialsPlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new AllOfficialsHandler() };
    }
}