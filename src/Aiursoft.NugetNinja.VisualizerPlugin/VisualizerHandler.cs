using Aiursoft.Canon;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NugetNinja.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

public sealed class VisualizerHandler : CommandHandler
{
    public override string Name => "visualize";

    public override string Description => "The command to visualize the dependency relationship, with mermaid markdown.";

    private static readonly Option<int> DepthOption = new(
        new[] { "--depth", "-d" },
        () => int.MaxValue,
        "Depth for package reference");

    private static readonly Option<string> ExcludeOption = new(
        new[] { "--excludes" },
        "Packages to exclude from the chart. Seperated by ','. For example: 'Microsoft,System,Test' to ignore system packages.");

    private static readonly Option<bool> LocalOnly = new(
        new[] { "--local", "-l" },
        () => false,
        "Only show local project references. (Ignore package references.)");

    public override Option[] GetCommandOptions()
    {
        return new Option[]
        {
            DepthOption,
            ExcludeOption,
            LocalOnly
        };
    }

    public override void OnCommandBuilt(Command command)
    {
        command.SetHandler(
            Execute,
            OptionsProvider.PathOptions,
            DepthOption,
            ExcludeOption,
            LocalOnly,
            OptionsProvider.VerboseOption,
            OptionsProvider.CustomNugetServer,
            OptionsProvider.PatToken);
    }

    private Task Execute(
        string path,
        int depth,
        string? excludes,
        bool localOnly,
        bool verbose,
        string customNugetServer,
        string patToken)
    {
        var host = BuildHost(verbose, customNugetServer, patToken);
        var excludesArray = excludes?
            .Split(',')
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct()
            .ToArray() ?? Array.Empty<string>();
        return RunFromHost(host, path, depth, excludesArray, localOnly);
    }

    private IHost BuildHost(
        bool verbose,
        string customNugetServer,
        string patToken)
    {
        var host = ServiceBuilder.CreateCommandHostBuilder<StartUp>(verbose);
        host.ConfigureServices(services =>
        {
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
        });
        return host.Build();
    }

    private Task RunFromHost(IHost host, string path, int depth, string[] excludes, bool localOnly)
    {
        var serviceProvider = host.Services;
        var service = serviceProvider.GetRequiredService<Entry>();
        var logger = serviceProvider.GetRequiredService<ILogger<Entry>>();

        var fullPath = Path.GetFullPath(path);
        logger.LogTrace(@"Starting service: '{Name}'. Full path is: '{FullPath}'", nameof(Entry), fullPath);
        return service.OnServiceStartedAsync(fullPath, depth, excludes, localOnly);
    }
}
