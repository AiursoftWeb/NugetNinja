using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.Services;

public class ExpectFilesDetector : IActionDetector
{
    private readonly HttpClient _http;
    private readonly ILogger<ExpectFilesDetector> _logger;

    public ExpectFilesDetector(
        HttpClient http,
        ILogger<ExpectFilesDetector> logger)
    {
        _http = http;
        _logger = logger;
    }
    
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        _logger.LogTrace("Analyzing expected {Count} files...", context.NinjaConfig.Files.Count);
        foreach (var fileExpected in context.NinjaConfig.Files)
        {
            if (!string.IsNullOrWhiteSpace(fileExpected.ContentUri) && !string.IsNullOrWhiteSpace(fileExpected.Name))
            {
                _logger.LogTrace("Inspecting file {Name} with URI {Uri}...", fileExpected.Name, fileExpected.ContentUri);
                var fileContentShouldBe = await _http.GetStringAsync(fileExpected.ContentUri);
                var pathFileShouldBe = Path.Combine(context.RootPath, fileExpected.Name);
                var fileOnDisk = new FileInfo(pathFileShouldBe);
                if (!fileOnDisk.Exists)
                {
                   // Write the content.
                    _logger.LogInformation("File {Name} does not exist. Creating...", fileExpected.Name);
                    yield return new PatchFileAction
                    {
                        FilePath = pathFileShouldBe,
                        Content = fileContentShouldBe,
                        SourceProject = context.AllProjects.First(),
                    };
                }
                else
                {
                    // Check the content.
                    var fileContentOnDisk = await File.ReadAllTextAsync(pathFileShouldBe);
                    if (fileContentOnDisk != fileContentShouldBe)
                    {
                        _logger.LogInformation("File {Name} is not the same as expected. Patching...", fileExpected.Name);
                        yield return new PatchFileAction
                        {
                            FilePath = pathFileShouldBe,
                            Content = fileContentShouldBe,
                            SourceProject = context.AllProjects.First(),
                        };
                    }
                    else
                    {
                        _logger.LogTrace("File {Name} is the same as expected. Skipping...", fileExpected.Name);
                    }
                }
            }
        }
    }
}