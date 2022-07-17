using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja;

public class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Trace);
        });

        services.AddSingleton<Entry>();
        services.AddTransient<Extractor>();
        services.AddTransient<UselessProjectReferenceDetector>();

        var serviceProvider = services.BuildServiceProvider();
        var entry = serviceProvider.GetRequiredService<Entry>();
        await entry.StartEntry(new string[] { @"C:\Users\AnduinXue\source\repos\AiursoftWeb\Infrastructures" });
    }
}