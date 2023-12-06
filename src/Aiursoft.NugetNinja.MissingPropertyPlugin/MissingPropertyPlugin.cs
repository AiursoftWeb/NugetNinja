using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyPlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new MissingPropertyHandler() };
    }
}