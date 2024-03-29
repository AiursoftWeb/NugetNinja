using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.Models;

public class PatchFileAction : IAction
{
    public required string FilePath { get; init; }
    public required string Content { get; init; }
    
    public string BuildMessage()
    {
        return $"The file {FilePath} should be patched.";
    }

    public async Task TakeActionAsync()
    {
        // Delete then write.
        File.Delete(FilePath);
        await File.WriteAllTextAsync(FilePath, Content);
    }

    public Project? SourceProject { get; init; }
}