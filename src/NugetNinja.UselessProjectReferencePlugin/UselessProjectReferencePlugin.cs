using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class UselessProjectReferencePlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new ProjectReferenceHandler() };
    }
}