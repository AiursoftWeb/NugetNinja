using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin;

public class PackageReferenceHandler : DetectorBasedCommandHandler<UselessPackageReferenceDetector, StartUp>
{
    protected override string Name => "clean-pkg";

    protected override string Description => "The command to clean up possible useless package references.";
}