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

        model.NinjaConfig = await ReadConfigAsync(rootPath);

        foreach (var csprojPath in csprojs)
        {
            await model.IncludeProject(csprojPath, logger);
        }

        return model;
    }

    public async Task<NinjaConfig> ReadConfigAsync(string rootPath)
    {
        var configFilePath = Path.Combine(rootPath, "ninja.yaml");
        if (!File.Exists(configFilePath))
        {
            logger.LogWarning("Can not find ninja config file: {Path}", configFilePath);
            return new NinjaConfig();
        }

        logger.LogTrace("Found and parsing ninja config file: {Path}", configFilePath);
        var configContent = await File.ReadAllTextAsync(configFilePath);
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var config = deserializer.Deserialize<NinjaConfig?>(configContent) ?? new NinjaConfig();
        config.Nuget?.Validate();
        config.ProposalCooldowns?.Validate();
        return config;
    }
}

public class NinjaConfig
{
    // ReSharper disable once UnusedMember.Global
    public int ConfigVersion { get; set; }

    // Retained for binary compatibility; use Http.Profiles for new configuration.
    public List<string> HttpProfiles { get; set; } = [];

    public NinjaHttpConfig? Http { get; set; } = new();

    public NinjaNpmConfig? Npm { get; set; }

    public NinjaNugetConfig? Nuget { get; set; }

    public NinjaProposalCooldowns? ProposalCooldowns { get; set; }
    
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<NinjaConfigFile> Files { get; set; } = [];
}

public class NinjaNpmConfig
{
    public bool UpdateDependencies { get; set; }
}

public class NinjaConfigFile
{
    public string? Name { get; set; }
    public string? ContentUri { get; set; }
}

public class NinjaHttpConfig
{
    public Dictionary<string, NinjaHttpProfile?> Profiles { get; set; } = [];
}

public class NinjaHttpProfile
{
    public string Origin { get; set; } = string.Empty;
    public Dictionary<string, string> HeadersFromEnv { get; set; } = [];
}
