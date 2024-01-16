using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.UselessProjectReferencePlugin.Services;

namespace Aiursoft.NugetNinja.UselessProjectReferencePlugin.App;

public class ProjectReferenceHandler : DetectorBasedCommandHandler<UselessProjectReferenceDetector, StartUp>
{
    protected override string Name => "clean-prj";

    protected override string Description => "The command to clean up possible useless project references.";
}