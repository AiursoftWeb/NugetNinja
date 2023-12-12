using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class MissingPropertyHandler : DetectorBasedCommandHandler<MissingPropertyDetector, StartUp>
{
    protected override string Name => "fill-properties";

    protected override string Description => "The command to fill all missing properties for .csproj files.";
}