using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyHandler : DetectorBasedCommandHandler<MissingPropertyDetector, StartUp>
{
    public override string Name => "fill-properties";

    public override string Description => "The command to fill all missing properties for .csproj files.";
}