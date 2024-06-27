using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aiursoft.NugetNinja.Core.Services.Extractor;

public class Extractor(ILogger<Extractor> logger)
{
    public async Task<Model.Workspace.Model> Parse(string rootPath)
    {
        logger.LogTrace("Parsing files to build project structure based on path: \'{Path}\'...", rootPath);
        var csprojs = Directory
            .EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
            .ToArray();

        var model = new Model.Workspace.Model
        {
            RootPath = rootPath
        };

        foreach (var csprojPath in csprojs)
        {
            await model.IncludeProject(csprojPath, logger);
        }
        
        var configFilePath = Path.Combine(rootPath, "ninja.yaml");
        if (File.Exists(configFilePath))
        {
            logger.LogTrace("Found and parsing ninja config file: {Path}", configFilePath);
            var configContent = await File.ReadAllTextAsync(configFilePath);
            
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var deserialized = deserializer.Deserialize<NinjaConfig>(configContent);
            model.NinjaConfig = deserialized;
        }
        else
        {
            logger.LogWarning("Can not find ninja config file: {Path}", configFilePath);
        }

        return model;
    }
}

public class NinjaConfig
{
    // ReSharper disable once UnusedMember.Global
    public int ConfigVersion { get; set; }
    
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<NinjaConfigFile> Files { get; set; } = [];
}

public class NinjaConfigFile
{
    public string? Name { get; set; }
    public string? ContentUri { get; set; }
}