using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.MissingPropertyPlugin.Services;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.App;

public class MissingPropertyHandler : DetectorBasedCommandHandler<MissingPropertyDetector, StartUp>
{
    protected override string Name => "fill-properties";

    protected override string Description => "The command to fill all missing properties for .csproj files.";
}