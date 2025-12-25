using Aiursoft.Canon;
using Aiursoft.NugetNinja.Core.Services.Utils;
using Aiursoft.NugetNinja.GitServerBase.Models.Abstractions;
using Aiursoft.NugetNinja.GitServerBase.Models.Configuration;
using Aiursoft.NugetNinja.GitServerBase.Models.Providers;
using Aiursoft.NugetNinja.MergeBot;
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
            services.AddTaskCanon();
            services.AddHttpClient();
            services.Configure<List<MergeServer>>(context.Configuration.GetSection("Servers"));
            services.AddTransient<Entry>();
            services.AddTransient<HttpWrapper>();
            services.AddTransient<IGitServer, GitLabService>();
        });
}