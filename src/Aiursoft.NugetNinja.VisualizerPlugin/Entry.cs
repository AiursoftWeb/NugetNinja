using Aiursoft.NugetNinja.Core;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

public class Entry : IEntryService
{
    private readonly Extractor _extractor;

    public Entry(Extractor extractor)
    {
        _extractor = extractor;
    }
    
    public async Task OnServiceStartedAsync(string path, bool shouldTakeAction)
    {
        var model = await _extractor.Parse(path);
        Console.WriteLine(model.AllProjects.Count);
    }
}