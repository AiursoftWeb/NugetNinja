using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.Services;

public class ExpectFilesDetector : IActionDetector
{
    private static readonly HttpClient DefaultAuthenticatedClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false
    });
    private readonly HttpClient _http;
    private readonly HttpClient _authenticatedHttp;
    private readonly ILogger<ExpectFilesDetector> _logger;

    public ExpectFilesDetector(HttpClient http, ILogger<ExpectFilesDetector> logger)
        : this(http, logger, DefaultAuthenticatedClient)
    {
    }

    public ExpectFilesDetector(HttpClient http, ILogger<ExpectFilesDetector> logger, HttpClient authenticatedHttp)
    {
        _http = http;
        _logger = logger;
        _authenticatedHttp = authenticatedHttp;
    }

    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        var downloader = new ExpectedContentDownloader(_http, _authenticatedHttp, context.NinjaConfig);
        _logger.LogTrace("Analyzing expected {Count} files...", context.NinjaConfig.Files.Count);

        foreach (var fileExpected in context.NinjaConfig.Files)
        {
            if (string.IsNullOrWhiteSpace(fileExpected.Name))
            {
                _logger.LogWarning("File expected with empty Name is skipped.");
                continue;
            }

            var expectedName = fileExpected.Name;
            var expectedPath = Path.Combine(context.RootPath, expectedName);
            var directory = new DirectoryInfo(context.RootPath);

            var matches = directory.GetFiles()
                .Where(f => string.Equals(f.Name, expectedName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count > 1)
            {
                _logger.LogError("Multiple files matching {ExpectedName} (ignoring case) found in {Directory}.", expectedName, context.RootPath);
                throw new Exception($"Multiple files matching {expectedName} (ignoring case) found in {context.RootPath}.");
            }
            if (matches.Count == 1)
            {
                var existingFile = matches[0];
                var isExactName = string.Equals(existingFile.Name, expectedName, StringComparison.Ordinal);

                if (!isExactName)
                {
                    _logger.LogInformation("File {ExistingName} found (ignoring case match for {ExpectedName}). Renaming...", existingFile.Name, expectedName);
                    yield return new RenameFileAction
                    {
                        SourcePath = existingFile.FullName,
                        DestinationPath = expectedPath,
                    };
                }

                if (!string.IsNullOrWhiteSpace(fileExpected.ContentUri))
                {
                    var expectedContent = await downloader.GetStringAsync(fileExpected.ContentUri);
                    string actualContent;
                    try
                    {
                        actualContent = await File.ReadAllTextAsync(existingFile.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to read file content from {FilePath}", existingFile.FullName);
                        actualContent = string.Empty;
                    }

                    if (actualContent != expectedContent)
                    {
                        _logger.LogInformation("File {ExpectedName} content does not match expected content. Patching...", expectedName);
                        yield return new PatchFileAction
                        {
                            FilePath = expectedPath,
                            Content = expectedContent,
                        };
                    }
                    else
                    {
                        _logger.LogTrace("File {ExpectedName} content matches expected. No patch needed.", expectedName);
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(fileExpected.ContentUri))
                {
                    var expectedContent = await downloader.GetStringAsync(fileExpected.ContentUri);
                    _logger.LogInformation("File {ExpectedName} does not exist. Creating with expected content...", expectedName);
                    yield return new PatchFileAction
                    {
                        FilePath = expectedPath,
                        Content = expectedContent,
                    };
                }
                else
                {
                    _logger.LogInformation("File {ExpectedName} does not exist. Creating an empty file...", expectedName);
                    yield return new PatchFileAction
                    {
                        FilePath = expectedPath,
                        Content = string.Empty,
                    };
                }
            }
        }
    }
}
