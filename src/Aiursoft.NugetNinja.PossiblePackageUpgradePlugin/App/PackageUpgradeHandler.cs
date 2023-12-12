using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin;

public class PackageUpgradeHandler : DetectorBasedCommandHandler<PackageReferenceUpgradeDetector, StartUp>
{
    protected override string Name => "upgrade-pkg";

    protected override string Description =>
        "The command to upgrade all package references to possible latest and avoid conflicts.";
}