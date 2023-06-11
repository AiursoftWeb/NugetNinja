﻿using System.CommandLine;
using Aiursoft.Canon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.Core;

public abstract class ServiceCommandHandler<TE, TS> : CommandHandler
    where TE : class, IEntryService
    where TS : class, IStartUp, new()
{
    public override void OnCommandBuilt(Command command)
    {
        command.SetHandler(
            Execute,
            OptionsProvider.PathOptions,
            OptionsProvider.DryRunOption,
            OptionsProvider.VerboseOption,
            OptionsProvider.AllowPreviewOption,
            OptionsProvider.CustomNugetServer,
            OptionsProvider.PatToken,
            OptionsProvider.AllowPackageVersionCrossMicrosoftRuntime);
    }

    public Task Execute(
        string path, 
        bool dryRun, 
        bool verbose, 
        bool allowPreview, 
        string customNugetServer,
        string patToken,
        bool allowCross)
    {
        var services = BuildServices(verbose, allowPreview, customNugetServer, patToken, allowCross);
        return RunFromServices(services, path, dryRun);
    }

    protected virtual ServiceCollection BuildServices(
        bool verbose, 
        bool allowPreview, 
        string customNugetServer,
        string patToken,
        bool allowCross)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging
                .AddFilter("Microsoft.Extensions", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning);
            logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = verbose;
                options.SingleLine = true;
                options.TimestampFormat = "mm:ss ";
            });
            logging.SetMinimumLevel(verbose ? LogLevel.Trace : LogLevel.Warning);
        });

        var startUp = new TS();
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddTaskCanon();
        services.AddTransient<Extractor>();
        services.AddTransient<ProjectsEnumerator>();
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

        startUp.ConfigureServices(services);
        services.AddTransient<TE>();
        return services;
    }

    protected virtual Task RunFromServices(ServiceCollection services, string path, bool dryRun)
    {
        var serviceProvider = services.BuildServiceProvider();
        var service = serviceProvider.GetRequiredService<TE>();
        var logger = serviceProvider.GetRequiredService<ILogger<TE>>();

        var fullPath = Path.GetFullPath(path);
        logger.LogTrace(@"Starting service: '{Name}'. Full path is: '{FullPath}', Dry run is: '{DryRun}'", typeof(TE).Name, fullPath, dryRun);
        return service.OnServiceStartedAsync(fullPath, !dryRun);
    }
}