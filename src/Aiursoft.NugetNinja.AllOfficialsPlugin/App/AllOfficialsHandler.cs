using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin;

public class AllOfficialsHandler : ServiceCommandHandler<RunAllOfficialPluginsService, StartUp>
{
    protected override string Name => "all-officials";

    protected override string Description => "The command to run all officially supported features.";

    protected override string[] Alias => new[] { "all" };
}