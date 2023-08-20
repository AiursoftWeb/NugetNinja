using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class AllOfficialsHandler : ServiceCommandHandler<RunAllOfficialPluginsService, StartUp>
{
    public override string Name => "all-officials";

    public override string Description => "The command to run all officially supported features.";

    public override string[] Alias => new[] { "all" };
}