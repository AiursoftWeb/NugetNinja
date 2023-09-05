using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin;

public class ResetRuntime : IAction
{
    private readonly int _deprecated;
    private readonly int _inserted;

    public ResetRuntime(
        Project project,
        string[] newRuntimes,
        int inserted,
        int deprecated)
    {
        _inserted = inserted;
        _deprecated = deprecated;
        SourceProject = project;
        NewRuntimes = newRuntimes;
    }

    public Project SourceProject { get; }
    public string[] NewRuntimes { get; }

    public string BuildMessage()
    {
        return
            $"The project: '{SourceProject}' with runtimes: '{string.Join(',', SourceProject.GetTargetFrameworks())}' should insert {_inserted} runtime(s) and deprecate {_deprecated} runtime(s) to '{string.Join(',', NewRuntimes)}'.";
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