using System.CommandLine;
using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

public class VisualizerPlugin : IPlugin
{
    public CommandHandler[] Install()
    {
        return new CommandHandler[] { new VisualizerHandler() };
    }
}

public sealed class VisualizerHandler : CommandHandler
{
    public override string Name => "visualize";

    public override string Description => "The command to visualize the dependency relationship, with mermaid markdown.";

    private static readonly Option<int> DepthOption = new(
        new[] { "--depth", "-d" },
        () => 1,
        "Depth for package reference");
    
    public override Option[] GetCommandOptions()
    {
        return new Option[] { DepthOption };
    }

    public override void OnCommandBuilt(Command command)
    {
        command.SetHandler(
            Execute,
            OptionsProvider.PathOptions,
            DepthOption,
            OptionsProvider.VerboseOption,
            OptionsProvider.CustomNugetServer,
            OptionsProvider.PatToken);
    }

    private Task Execute(
        string path, 
        int depth,
        bool verbose,
        string customNugetServer,
        string patToken)
    {
        var services = BuildServices(verbose, customNugetServer, patToken);
        return RunFromServices(services, path, depth);
    }

    private ServiceCollection BuildServices(
        bool verbose, 
        string customNugetServer,
        string patToken)
    {
        var services = ServiceBuilder.BuildServices<StartUp>(verbose);
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTaskCanon();
        services.AddTransient<Extractor>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<NugetService>();
        services.AddTransient<VersionCrossChecker>();
        services.Configure<AppSettings>(options =>
        {
            options.Verbose = verbose;
            options.CustomNugetServer = customNugetServer;
            options.PatToken = patToken;
        });
        services.AddTransient<Entry>();
        return services;
    }

    private Task RunFromServices(ServiceCollection services, string path, int depth)
    {
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<Entry>();
        var logger = serviceProvider.GetRequiredService<ILogger<Entry>>();

        var fullPath = Path.GetFullPath(path);
        logger.LogTrace(@"Starting service: '{Name}'. Full path is: '{FullPath}'", nameof(Entry), fullPath);
        return service.OnServiceStartedAsync(fullPath, depth);
    }
}