using System.CommandLine;
using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.IO;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.Core.Model.Framework;

public abstract class ServiceCommandHandler<TE, TS> : ExecutableCommandHandlerBuilder
    where TE : class, IEntryService
    where TS : class, IStartUp, new()
{
    protected override async Task Execute(ParseResult context)
    {
        var path = context.GetValue(OptionsProvider.PathOptions)!;
        var dryRun = context.GetValue(OptionsProvider.DryRunOption);
        var verbose = context.GetValue(OptionsProvider.VerboseOption);
        var allowPreview = context.GetValue(OptionsProvider.AllowPreviewOption);
        var customNugetServer = context.GetValue(OptionsProvider.CustomNugetServerOption)!;
        var patToken = context.GetValue(OptionsProvider.PatTokenOption)!;
        var allowCross = context.GetValue(OptionsProvider.AllowPreviewOption);

        await ExecuteWithArgs(path, dryRun, verbose, allowPreview, customNugetServer, patToken, allowCross);
    }

    private Task ExecuteWithArgs(
        string path,
        bool dryRun,
        bool verbose,
        bool allowPreview,
        string customNugetServer,
        string patToken,
        bool allowCross)
    {
        var host = BuildHost(verbose, allowPreview, customNugetServer, patToken, allowCross);
        return RunFromHost(host, path, dryRun);
    }

    protected virtual IHost BuildHost(
        bool verbose,
        bool allowPreview,
        string customNugetServer,
        string patToken,
        bool allowCross)
    {
        var hostBuilder = ServiceBuilder.CreateCommandHostBuilder<TS>(verbose);
        hostBuilder.ConfigureServices(services =>
        {
            services.AddMemoryCache();
            services.AddHttpClient();
            services.AddTaskCanon();
            services.AddTransient<Extractor>();
            services.AddTransient<ProjectsEnumerator>();
            services.AddTransient<CsprojWriter>();
            services.AddTransient<NugetService>();
            services.AddTransient<VersionCrossChecker>();
            services.Configure<AppSettings>(options =>
            {
                options.AllowCross = allowCross;
                options.Verbose = verbose;
                options.AllowPreview = allowPreview;
                options.CustomNugetServer = customNugetServer;
                options.PatToken = patToken;
            });
            services.AddTransient<TE>();
        });
        return hostBuilder.Build();
    }

    protected virtual Task RunFromHost(IHost host, string path, bool dryRun)
    {
        var serviceProvider = host.Services;
        var service = serviceProvider.GetRequiredService<TE>();
        var logger = serviceProvider.GetRequiredService<ILogger<TE>>();

        var fullPath = Path.GetFullPath(path);
        logger.LogTrace(@"Starting service: '{Name}'. Full path is: '{FullPath}', Dry run is: '{DryRun}'", typeof(TE).Name, fullPath, dryRun);
        return service.OnServiceStartedAsync(fullPath, !dryRun);
    }
}
