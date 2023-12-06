using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

public class VisualizerPlugin : IPlugin
{
    public ICommandHandlerBuilder[] Install()
    {
        return new ICommandHandlerBuilder[] { new VisualizerHandler() };
    }
}
