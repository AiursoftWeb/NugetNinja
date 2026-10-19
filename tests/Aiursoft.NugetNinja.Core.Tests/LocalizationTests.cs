using System.Xml.Linq;
using Aiursoft.Dotlang.AspNetTranslate;
using Aiursoft.GptClient.Abstractions;
using Aiursoft.GptClient.Services;
using Aiursoft.NugetNinja.PrBot.Configuration;
using Aiursoft.NugetNinja.PrBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class LocalizationTests
{
    [TestMethod]
    public async Task CanceledRepository_DoesNotPreventNextRepositoryFromWritingTranslations()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = await CreateProject(directory, "First", "Canceled first", "Canceled second", "Canceled third", "Canceled fourth");
            var second = await CreateProject(directory, "Second", "Healthy translation");
            var logger = new LocalizationLogger();
            await using var services = CreateServices(logger);
            var localization = services.GetRequiredService<LocalizationService>();

            Assert.IsFalse(await localization.LocalizeProjectAsync(first));
            Assert.IsTrue(await localization.LocalizeProjectAsync(second),
                "The next repository must not execute canceled work from the previous repository.");

            var resource = XDocument.Load(Path.Combine(second, "Resources", "Sample.zh-CN.resx"));
            var entry = resource.Descendants("data").Single(element => element.Attribute("name")?.Value == "Healthy translation");
            Assert.AreEqual("Translated content", entry.Element("value")?.Value);
            Assert.IsTrue(logger.Messages.Any(message => message.Contains("canceled before the repository timeout", StringComparison.Ordinal)),
                "An upstream cancellation must not be reported as this repository exhausting its 25-minute budget.");
            Assert.IsFalse(logger.Messages.Any(message => message.Contains("No projects with Resources", StringComparison.Ordinal)),
                "A canceled translation still had a Resources directory.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task RepositoryWithoutResources_SkipsLocalization()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Sample.csproj"), "<Project />");
            var logger = new LocalizationLogger();
            await using var services = CreateServices(logger);

            Assert.IsFalse(await services.GetRequiredService<LocalizationService>().LocalizeProjectAsync(directory));
            Assert.IsTrue(logger.Messages.Any(message => message.Contains("No projects with Resources", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<string> CreateProject(string directory, string name, params string[] keys)
    {
        var path = Path.Combine(directory, name);
        Directory.CreateDirectory(Path.Combine(path, "Resources"));
        await File.WriteAllTextAsync(Path.Combine(path, "Sample.csproj"), "<Project />");
        var expressions = string.Join("\n", keys.Select(key => $"_ = Localizer[\"{key}\"];"));
        await File.WriteAllTextAsync(Path.Combine(path, "Sample.cs"), $"class Sample {{ void Render() {{ {expressions} }} }}");
        return path;
    }

    private static ServiceProvider CreateServices(LocalizationLogger logger)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        new StartUp().ConfigureServices(services);
        services.Configure<PrBotOptions>(options =>
        {
            options.LocalizationEnabled = true;
            options.OllamaApiEndpoint = "https://translation.invalid/api/chat";
            options.OllamaModel = "test";
            options.OllamaApiKey = "test";
            options.LocalizationTargetLanguages = ["zh-CN"];
            options.LocalizationConcurrentRequests = 2;
        });
        services.AddSingleton<ChatClient>(provider => new TranslationClient(provider.GetRequiredService<HttpClient>()));
        services.AddSingleton<ILogger<LocalizationService>>(logger);
        services.AddTransient<LocalizationService>();
        return services.BuildServiceProvider();
    }

    private sealed class TranslationClient(HttpClient client) : ChatClient(client, NullLogger<ChatClient>.Instance)
    {
        public override Task<CompletionData> AskModel(OpenAiRequestModel model, string completionApiUrl, string? token,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (model.Messages.Any(message => message.Content?.Contains("Canceled first", StringComparison.Ordinal) == true))
            {
                return Task.FromCanceled<CompletionData>(new CancellationToken(canceled: true));
            }

            var result = new CompletionData();
            result.SetContent("Translated content");
            return Task.FromResult(result);
        }
    }

    private sealed class LocalizationLogger : ILogger<LocalizationService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
