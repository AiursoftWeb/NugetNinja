using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.Services;

public class ExpectFilesDetector(
    HttpClient http,
    ILogger<ExpectFilesDetector> logger) : IActionDetector
{
    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        logger.LogTrace("Analyzing expected {Count} files...", context.NinjaConfig.Files.Count);

        foreach (var fileExpected in context.NinjaConfig.Files)
        {
            if (string.IsNullOrWhiteSpace(fileExpected.Name))
            {
                logger.LogWarning("File expected with empty Name is skipped.");
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
                logger.LogError("Multiple files matching {ExpectedName} (ignoring case) found in {Directory}.", expectedName, context.RootPath);
                throw new Exception($"Multiple files matching {expectedName} (ignoring case) found in {context.RootPath}.");
            }
            if (matches.Count == 1)
            {
                var existingFile = matches[0];
                var isExactName = string.Equals(existingFile.Name, expectedName, StringComparison.Ordinal);

                if (!isExactName)
                {
                    logger.LogInformation("File {ExistingName} found (ignoring case match for {ExpectedName}). Renaming to {ExpectedName}.", 
                        existingFile.Name, expectedName, expectedName);
                    yield return new RenameFileAction
                    {
                        SourcePath = existingFile.FullName,
                        DestinationPath = expectedPath,
                    };
                }

                if (!string.IsNullOrWhiteSpace(fileExpected.ContentUri))
                {
                    var expectedContent = await http.GetStringAsync(fileExpected.ContentUri);
                    string actualContent;
                    try
                    {
                        actualContent = await File.ReadAllTextAsync(existingFile.FullName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to read file content from {FilePath}", existingFile.FullName);
                        actualContent = string.Empty;
                    }

                    if (actualContent != expectedContent)
                    {
                        logger.LogInformation("File {ExpectedName} content does not match expected content. Patching...", expectedName);
                        yield return new PatchFileAction
                        {
                            FilePath = expectedPath,
                            Content = expectedContent,
                        };
                    }
                    else
                    {
                        logger.LogTrace("File {ExpectedName} content matches expected. No patch needed.", expectedName);
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(fileExpected.ContentUri))
                {
                    var expectedContent = await http.GetStringAsync(fileExpected.ContentUri);
                    logger.LogInformation("File {ExpectedName} does not exist. Creating with expected content...", expectedName);
                    yield return new PatchFileAction
                    {
                        FilePath = expectedPath,
                        Content = expectedContent,
                    };
                }
                else
                {
                    logger.LogInformation("File {ExpectedName} does not exist. Creating an empty file...", expectedName);
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
