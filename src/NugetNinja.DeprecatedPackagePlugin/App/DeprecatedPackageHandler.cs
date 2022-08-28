

using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class DeprecatedPackageHandler : DetectorBasedCommandHandler<DeprecatedPackageDetector, StartUp>
{
    public override string Name => "remove-deprecated";

    public override string Description => "The command to replace all deprecated packages to new packages.";
}
