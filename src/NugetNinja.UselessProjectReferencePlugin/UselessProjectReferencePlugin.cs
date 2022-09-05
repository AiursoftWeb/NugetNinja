

using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class UselessProjectReferencePlugin : INinjaPlugin
{
    public CommandHandler[] Install() => new CommandHandler[] { new ProjectReferenceHandler() };
}
