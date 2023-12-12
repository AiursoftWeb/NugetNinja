using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin;

public class DeprecatedPackageHandler : DetectorBasedCommandHandler<DeprecatedPackageDetector, StartUp>
{
    protected override string Name => "remove-deprecated";

    protected override string Description => "The command to replace all deprecated packages to new packages.";
}