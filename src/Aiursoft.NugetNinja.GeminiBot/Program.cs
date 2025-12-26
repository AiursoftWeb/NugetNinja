using Aiursoft.NugetNinja.GeminiBot;
using Aiursoft.GitRunner;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.GeminiBot.Configuration;
using Aiursoft.NugetNinja.GeminiBot.Services;
using Aiursoft.NugetNinja.GitServerBase.Models;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers.AzureDevOps;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers.Gitea;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers.GitHub;
using Aiursoft.NugetNinja.GitServerBase.Services.Providers.GitLab;
using Aiursoft.CSTools.Services;
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
            services.Configure<GeminiBotOptions>(context.Configuration.GetSection("GeminiBot"));
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
            services.AddTransient<WorkspaceManager>();
            services.AddTransient<CommandService>();
            services.AddTransient<IssueProcessor>();
            services.AddTransient<MergeRequestProcessor>();
            services.AddTransient<Entry>();
        });
}