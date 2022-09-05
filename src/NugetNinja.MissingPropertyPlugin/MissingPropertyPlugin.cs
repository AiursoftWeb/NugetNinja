

using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyPlugin : INinjaPlugin
{
    public CommandHandler[] Install() => new CommandHandler[] { new MissingPropertyHandler() };
}
