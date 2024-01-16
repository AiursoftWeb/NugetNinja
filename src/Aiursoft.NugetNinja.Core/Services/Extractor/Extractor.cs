using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.Core.Services.Extractor;

public class Extractor
{
    private readonly ILogger<Extractor> _logger;

    public Extractor(ILogger<Extractor> logger)
    {
        _logger = logger;
    }

    public async Task<Model.Workspace.Model> Parse(string rootPath)
    {
        var csprojs = Directory
            .EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
            .ToArray();

        var model = new Model.Workspace.Model();

        foreach (var csprojPath in csprojs)
        {
            await model.IncludeProject(csprojPath, _logger);
        }

        return model;
    }
}