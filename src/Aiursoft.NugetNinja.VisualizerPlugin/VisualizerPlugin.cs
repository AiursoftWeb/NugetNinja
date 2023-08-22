using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

public class VisualizerPlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new VisualizerHandler() };
    }
}

public class VisualizerHandler : ServiceCommandHandler<Entry, StartUp>
{
    public override string Name => "visualize";

    public override string Description => "The command to visualize the dependency relationship, with mermaid markdown.";
}