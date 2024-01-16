using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.App;

public class PackageUpgradeHandler : DetectorBasedCommandHandler<PackageReferenceUpgradeDetector, StartUp>
{
    protected override string Name => "upgrade-pkg";

    protected override string Description =>
        "The command to upgrade all package references to possible latest and avoid conflicts.";
}