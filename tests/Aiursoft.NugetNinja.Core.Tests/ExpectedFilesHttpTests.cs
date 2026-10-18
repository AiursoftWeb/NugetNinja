using System.Net;
using System.Text.Json;
using Aiursoft.NugetNinja.Core.Abstracts;
using WorkspaceModel = Aiursoft.NugetNinja.Core.Model.Workspace.Model;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.ExpectFilesPlugin.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
[DoNotParallelize]
public class ExpectedFilesHttpTests
{
    private const string SecretVariable = "NINJA_HTTP_TEST_SECRET";
    private string? _previousProfiles;
    private string? _previousSecret;
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _previousProfiles = Environment.GetEnvironmentVariable("NINJA_HTTP_PROFILES");
        _previousSecret = Environment.GetEnvironmentVariable(SecretVariable);
        _root = Path.Combine(Path.GetTempPath(), "ninja-http-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("NINJA_HTTP_PROFILES", null);
        Environment.SetEnvironmentVariable(SecretVariable, "first-fixture-value");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("NINJA_HTTP_PROFILES", _previousProfiles);
        Environment.SetEnvironmentVariable(SecretVariable, _previousSecret);
        Directory.Delete(_root, true);
    }

    private static void Configure(string origin = "https://source.example")
    {
        Environment.SetEnvironmentVariable("NINJA_HTTP_PROFILES", JsonSerializer.Serialize(new
        {
            source = new
            {
                origin,
                headersFromEnv = new Dictionary<string, string> { ["X-Source-Key"] = SecretVariable }
            }
        }));
    }

    private WorkspaceModel Workspace(string uri = "https://source.example/files/config", bool selectProfile = true)
    {
        return new WorkspaceModel
        {
            RootPath = _root,
            NinjaConfig = new NinjaConfig
            {
                HttpProfiles = selectProfile ? ["source"] : [],
                Files = [new NinjaConfigFile { Name = "config.txt", ContentUri = uri }]
            }
        };
    }

    private static async Task<List<IAction>> Analyze(ExpectFilesDetector detector, WorkspaceModel model)
    {
        var actions = new List<IAction>();
        await foreach (var action in detector.AnalyzeAsync(model))
        {
            actions.Add(action);
        }
        return actions;
    }

    [TestMethod]
    public async Task ExpectedFileUsesConfiguredHeaderAndConverges()
    {
        Configure();
        using var anonymousHandler = new FileHandler();
        using var authenticatedHandler = new FileHandler();
        using var anonymous = new HttpClient(anonymousHandler);
        using var authenticated = new HttpClient(authenticatedHandler);
        var detector = new ExpectFilesDetector(anonymous, NullLogger<ExpectFilesDetector>.Instance, authenticated);
        var actions = await Analyze(detector, Workspace());
        Assert.HasCount(1, actions);
        await actions[0].TakeActionAsync();
        Assert.AreEqual("expected file", await File.ReadAllTextAsync(Path.Combine(_root, "config.txt")));
        Assert.AreEqual("first-fixture-value", authenticatedHandler.Headers.Single());
        Assert.IsEmpty(anonymousHandler.Headers);
        Assert.IsFalse(authenticated.DefaultRequestHeaders.Contains("X-Source-Key"));
        Assert.IsEmpty(await Analyze(detector, Workspace()));
    }

    [TestMethod]
    public async Task SubsequentAnalysisUsesCurrentCredential()
    {
        Configure();
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Analyze(detector, Workspace());
        Environment.SetEnvironmentVariable(SecretVariable, "rotated-fixture-value");
        await Analyze(detector, Workspace());
        CollectionAssert.AreEqual(new[] { "first-fixture-value", "rotated-fixture-value" }, handler.Headers);
    }

    [TestMethod]
    [DataRow("https://other.example/files/config")]
    [DataRow("https://source.example:444/files/config")]
    [DataRow("http://source.example/files/config")]
    public async Task OtherOriginsNeverReceiveConfiguredHeaders(string uri)
    {
        Configure();
        using var anonymousHandler = new FileHandler();
        using var authenticatedHandler = new FileHandler();
        using var anonymous = new HttpClient(anonymousHandler);
        using var authenticated = new HttpClient(authenticatedHandler);
        var detector = new ExpectFilesDetector(anonymous, NullLogger<ExpectFilesDetector>.Instance, authenticated);
        Assert.HasCount(1, await Analyze(detector, Workspace(uri)));
        Assert.HasCount(1, anonymousHandler.Headers);
        Assert.IsNull(anonymousHandler.Headers[0]);
        Assert.IsEmpty(authenticatedHandler.Headers);
    }

    [TestMethod]
    public async Task LegacyAnonymousConfigurationStillDownloads()
    {
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance);
        Assert.HasCount(1, await Analyze(detector, Workspace(selectProfile: false)));
        Assert.IsNull(handler.Headers.Single());
    }

    [TestMethod]
    [DataRow(302)]
    [DataRow(401)]
    [DataRow(403)]
    public async Task FailedAuthenticatedDownloadsProduceNoFileActions(int status)
    {
        Configure();
        using var handler = new FileHandler((HttpStatusCode)status);
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Assert.ThrowsAsync<HttpRequestException>(() => Analyze(detector, Workspace()));
        Assert.IsFalse(File.Exists(Path.Combine(_root, "config.txt")));
        Assert.HasCount(1, handler.Headers);
    }

    [TestMethod]
    public async Task MissingCredentialFailsBeforeDownloading()
    {
        Configure();
        Environment.SetEnvironmentVariable(SecretVariable, null);
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, Workspace()));
        Assert.IsEmpty(handler.Headers);
    }

    [TestMethod]
    public async Task MissingProfileNeverFallsBackToAnonymous()
    {
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, Workspace()));
        Assert.IsEmpty(handler.Headers);
    }

    [TestMethod]
    public async Task ProtectedOriginRequiresExplicitProfileSelection()
    {
        Configure();
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, Workspace(selectProfile: false)));
        Assert.IsEmpty(handler.Headers);
    }

    [TestMethod]
    public async Task InvalidRuntimeJsonDoesNotExposeConfigurationValues()
    {
        Environment.SetEnvironmentVariable("NINJA_HTTP_PROFILES", "{\"private-value-must-not-appear\":");
        using var client = new HttpClient(new FileHandler());
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, Workspace()));
        Assert.DoesNotContain("private-value-must-not-appear", error.ToString());
    }

    [TestMethod]
    [DataRow("http://source.example")]
    [DataRow("https://source.example/path")]
    [DataRow("https://source.example?query=value")]
    public async Task AuthenticationProfilesRequireHttpsOrigins(string origin)
    {
        Configure(origin);
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, Workspace()));
        Assert.IsEmpty(handler.Headers);
    }

    [TestMethod]
    public async Task ExtractorReadsOptionalProfileNames()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "ninja.yaml"), """
            configVersion: 1
            httpProfiles:
              - source
            files:
              - name: config.txt
                contentUri: https://source.example/files/config
            """);
        var model = await new Extractor(NullLogger<Extractor>.Instance).Parse(_root);
        CollectionAssert.AreEqual(new[] { "source" }, model.NinjaConfig.HttpProfiles);
        Assert.AreEqual("config.txt", model.NinjaConfig.Files.Single().Name);
    }

    private sealed class FileHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string?> Headers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Headers.Add(request.Headers.TryGetValues("X-Source-Key", out var values) ? values.Single() : null);
            var response = new HttpResponseMessage(status) { Content = new StringContent("expected file") };
            if (status == HttpStatusCode.Found)
            {
                response.Headers.Location = new Uri("https://other.example/sign-in");
            }
            return Task.FromResult(response);
        }
    }
}
