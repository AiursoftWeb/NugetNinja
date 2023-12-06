using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyPlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new MissingPropertyHandler() };
    }
}