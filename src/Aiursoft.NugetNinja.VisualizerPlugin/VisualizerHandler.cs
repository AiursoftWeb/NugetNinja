using Aiursoft.Canon;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Nuget;

namespace Aiursoft.NugetNinja.VisualizerPlugin;

public sealed class VisualizerHandler : ExecutableCommandHandlerBuilder
{
    protected override string Name => "visualize";

    protected override string Description => "The command to visualize the dependency relationship, with mermaid markdown.";

    private static readonly Option<int> DepthOption = new(
        name: "--depth",
        aliases: "-d")
    {
        Description = "Depth for package reference",
        DefaultValueFactory = _ => int.MaxValue,
    };

    private static readonly Option<string> ExcludeOption = new(
        "--excludes")
    {
        Description =
            "Packages to exclude from the chart. Seperated by ','. For example: 'Microsoft,System,Test' to ignore system packages."
    };

    private static readonly Option<bool> LocalOnlyOption = new(
        name: "--local",
        aliases: "-l")
    {
        Description = "Only show local project references. (Ignore package references.)",
        DefaultValueFactory = _ => false
    };

    protected override Option[] GetCommandOptions()
    {
        return
        [
            DepthOption,
            ExcludeOption,
            LocalOnlyOption
        ];
    }

    protected override Task Execute(ParseResult context)
    {
        var path = context.GetValue(OptionsProvider.PathOptions)!;
        var depth = context.GetValue(DepthOption);
        var excludes = context.GetValue(ExcludeOption);
        var localOnly = context.GetValue(LocalOnlyOption);
        var verbose = context.GetValue(OptionsProvider.VerboseOption);
        var customNugetServer = context.GetValue(OptionsProvider.CustomNugetServerOption)!;
        var patToken = context.GetValue(OptionsProvider.PatTokenOption)!;

        var host = BuildHost(verbose, customNugetServer, patToken);
        var excludesArray = excludes?
            .Split(',')
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct()
            .ToArray() ?? [];
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
