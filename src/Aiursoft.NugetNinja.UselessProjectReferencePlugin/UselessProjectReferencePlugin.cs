using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class UselessProjectReferencePlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new ProjectReferenceHandler() };
    }
}