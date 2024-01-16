using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.UselessPackageReferencePlugin.Services;

namespace Aiursoft.NugetNinja.UselessPackageReferencePlugin.App;

public class PackageReferenceHandler : DetectorBasedCommandHandler<UselessPackageReferenceDetector, StartUp>
{
    protected override string Name => "clean-pkg";

    protected override string Description => "The command to clean up possible useless package references.";
}