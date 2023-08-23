using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

public class VisualizerPlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new VisualizerHandler() };
    }
}
