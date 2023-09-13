using System.CommandLine;
using Aiursoft.Canon;
using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.CommandFramework.Framework;
using Aiursoft.CommandFramework.Services;
using Aiursoft.NugetNinja.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    private Task Execute(
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
        var hostBuilder = ServiceBuilder.BuildHost<TS>(verbose);
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