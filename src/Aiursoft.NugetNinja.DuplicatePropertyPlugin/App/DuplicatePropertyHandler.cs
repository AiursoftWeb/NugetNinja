using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.DuplicatePropertyPlugin.Services;

namespace Aiursoft.NugetNinja.DuplicatePropertyPlugin.App;

public class DuplicatePropertyHandler : DetectorBasedCommandHandler<DuplicatePropertyDetector, StartUp>
{
    protected override string Name => "clean-dup";

    protected override string Description => "The command to clean all duplicate properties for .csproj files.";
}