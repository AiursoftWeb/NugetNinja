using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.Core.Model.Framework;

namespace Aiursoft.NugetNinja.AllOfficialsPlugin.App;

public class AllOfficialsHandler : ServiceCommandHandler<RunAllOfficialPluginsService, StartUp>
{
    protected override string Name => "all-officials";

    protected override string Description => "The command to run all officially supported features.";

    protected override string[] Alias => ["all"];
}
