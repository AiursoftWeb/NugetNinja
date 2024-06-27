using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Models;

public class ResetRuntime(
    Project project,
    string[] newRuntimes,
    int inserted,
    int deprecated)
    : IAction
{
    public Project SourceProject { get; } = project;
    public string[] NewRuntimes { get; } = newRuntimes;

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' with runtimes: '{string.Join(',', SourceProject.GetTargetFrameworks())}' should insert {inserted} runtime(s) and deprecate {deprecated} runtime(s) to '{string.Join(',', NewRuntimes)}'.";
    }

    public async Task TakeActionAsync()
    {
        if (NewRuntimes.Length > 1)
        {
            await SourceProject.AddOrUpdateProperty(nameof(SourceProject.TargetFrameworks), string.Join(';', NewRuntimes));
            await SourceProject.RemoveProperty(nameof(SourceProject.TargetFramework));
        }
        else
        {
            await SourceProject.AddOrUpdateProperty(nameof(SourceProject.TargetFramework),
                NewRuntimes.FirstOrDefault() ?? string.Empty);
            await SourceProject.RemoveProperty(nameof(SourceProject.TargetFrameworks));
        }
    }
}