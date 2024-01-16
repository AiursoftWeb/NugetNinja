using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.DeprecatedPackagePlugin.Services;

namespace Aiursoft.NugetNinja.DeprecatedPackagePlugin.App;

public class DeprecatedPackageHandler : DetectorBasedCommandHandler<DeprecatedPackageDetector, StartUp>
{
    protected override string Name => "remove-deprecated";

    protected override string Description => "The command to replace all deprecated packages to new packages.";
}