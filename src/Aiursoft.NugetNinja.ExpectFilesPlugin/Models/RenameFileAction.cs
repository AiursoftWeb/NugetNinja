using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.ExpectFilesPlugin.Models;

public class RenameFileAction : IAction
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    
    public string BuildMessage()
    {
        return $"The file {SourcePath} will be renamed to {DestinationPath}.";
    }
    
    public async Task TakeActionAsync()
    {
        var destinationDir = Path.GetDirectoryName(DestinationPath)!;
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }
        File.Move(SourcePath, DestinationPath);
        await Task.CompletedTask;
    }
    
    public Project? SourceProject { get; init; }
}