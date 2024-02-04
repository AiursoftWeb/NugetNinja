using Aiursoft.NugetNinja.AllOfficialsPlugin;
using Aiursoft.NugetNinja.PrBot;
using Aiursoft.GitRunner;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.PrBot.Models;
using Aiursoft.NugetNinja.PrBot.Services.Providers;
using Aiursoft.NugetNinja.PrBot.Services.Providers.AzureDevOps;
using Aiursoft.NugetNinja.PrBot.Services.Providers.Gitea;
using Aiursoft.NugetNinja.PrBot.Services.Providers.GitHub;
using Aiursoft.NugetNinja.PrBot.Services.Providers.GitLab;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            services.AddGitRunner();
            services.AddTransient<Extractor>();
            services.AddTransient<ProjectsEnumerator>();
            services.AddTransient<IVersionControlService, GitHubService>();
            services.AddTransient<IVersionControlService, GiteaService>();
            services.AddTransient<IVersionControlService, AzureDevOpsService>();
            services.AddTransient<IVersionControlService, GitLabService>();
            services.AddTransient<HttpWrapper>();
            services.AddTransient<NugetService>();
            services.AddTransient<VersionCrossChecker>();
            new StartUp().ConfigureServices(services);
            services.AddTransient<RunAllOfficialPluginsService>();
            services.AddTransient<Entry>();
        });
}