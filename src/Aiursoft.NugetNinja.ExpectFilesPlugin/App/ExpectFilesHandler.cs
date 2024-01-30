using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Services;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.App;

public class ExpectFilesHandler : DetectorBasedCommandHandler<ExpectFilesDetector, StartUp>
{
    protected override string Name => "expect-files";

    protected override string Description => "The command to search for all expected files and add patch the content.";
}