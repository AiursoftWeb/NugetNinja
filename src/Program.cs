using Aiursoft.NugetNinja.Abstracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

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

        var generators = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass)
            .Where(t => t.GetInterfaces().Contains(typeof(IActionGenerator)));

        foreach (var generator in generators)
        {
            services.AddTransient(typeof(IActionGenerator), generator);
        }

        services.AddTransient<Enumerator>();

        var serviceProvider = services.BuildServiceProvider();
        var entry = serviceProvider.GetRequiredService<Entry>();
        await entry.StartEntry(new string[] { @"D:\ModelASpaceport" });
    }
}