using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin;

public class ProjectReferenceHandler : DetectorBasedCommandHandler<UselessProjectReferenceDetector, StartUp>
{
    public override string Name => "clean-prj";

    public override string Description => "The command to clean up possible useless project references.";
}