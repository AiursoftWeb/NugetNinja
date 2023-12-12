using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class ProjectReferenceHandler : DetectorBasedCommandHandler<UselessProjectReferenceDetector, StartUp>
{
    protected override string Name => "clean-prj";

    protected override string Description => "The command to clean up possible useless project references.";
}