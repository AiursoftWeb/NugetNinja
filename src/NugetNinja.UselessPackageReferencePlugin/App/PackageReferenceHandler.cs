

using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class PackageReferenceHandler : DetectorBasedCommandHandler<UselessPackageReferenceDetector, StartUp>
{
    public override string Name => "clean-pkg";

    public override string Description => "The command to clean up possible useless package references.";
}
