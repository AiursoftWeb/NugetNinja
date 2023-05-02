using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Aiursoft.NugetNinja.AllOfficialsPlugin;
using Aiursoft.NugetNinja.Core;
using Aiursoft.NugetNinja.PrBot;

await CreateHostBuilder(args)
    .Build()
    .Services
    .GetRequiredService<Entry>()
    .RunAsync();

static IHostBuilder CreateHostBuilder(string[] args)
{
    return Host
        .CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            logging
                .AddFilter("Microsoft.Extensions", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning);
            logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = true;
                options.TimestampFormat = "mm:ss ";
            });
        })
        .ConfigureServices((context, services) =>
        {
            services.AddMemoryCache();
            services.AddHttpClient();
            services.Configure<List<Server>>(context.Configuration.GetSection("Servers"));
            services.AddSingleton<CacheService>();
            services.AddTransient<RetryEngine>();
            services.AddTransient<Extractor>();
            services.AddTransient<ProjectsEnumerator>();
            services.AddTransient<IVersionControlService, GitHubService>();
            services.AddTransient<IVersionControlService, GiteaService>();
            services.AddTransient<IVersionControlService, AzureDevOpsService>();
            services.AddTransient<NugetService>();
            services.AddTransient<CommandRunner>();
            services.AddTransient<WorkspaceManager>();
            new StartUp().ConfigureServices(services);
            services.AddTransient<RunAllOfficialPluginsService>();
            services.AddTransient<Entry>();
        });
}
