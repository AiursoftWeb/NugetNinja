using System.Net;
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
    private string? _previousOrigin;
    private string _profileOrigin = "https://source.example";
    private string? _previousSecret;
    private string _root = null!;

    [TestInitialize]
    public void Initialize()
    {
        _previousOrigin = Environment.GetEnvironmentVariable(SecretVariable + "_ORIGIN");
        _previousSecret = Environment.GetEnvironmentVariable(SecretVariable);
        _root = Path.Combine(Path.GetTempPath(), "ninja-http-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(SecretVariable + "_ORIGIN", "https://source.example");
        Environment.SetEnvironmentVariable(SecretVariable, "first-fixture-value");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(SecretVariable + "_ORIGIN", _previousOrigin);
        Environment.SetEnvironmentVariable(SecretVariable, _previousSecret);
        Directory.Delete(_root, true);
    }

    private void Configure(string origin = "https://source.example")
    {
        _profileOrigin = origin;
    }

    private WorkspaceModel Workspace(string uri = "https://source.example/files/config", bool selectProfile = true)
    {
        return new WorkspaceModel
        {
            RootPath = _root,
            NinjaConfig = new NinjaConfig
            {
                Http = new NinjaHttpConfig
                {
                    Profiles = selectProfile ? new Dictionary<string, NinjaHttpProfile?>
                    {
                        ["source"] = new()
                        {
                            Origin = _profileOrigin,
                            HeadersFromEnv = new Dictionary<string, string> { ["X-Source-Key"] = SecretVariable }
                        }
                    } : []
                },
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
    [DataRow("Date")]
    [DataRow("If-Modified-Since")]
    public async Task RepositoryHeaderNamesCannotExposeCredentialsThroughTypedParsers(string header)
    {
        using var handler = new FileHandler(headerName: header);
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        var model = Workspace();
        model.NinjaConfig.Http!.Profiles["source"]!.HeadersFromEnv = new Dictionary<string, string>
        {
            [header] = SecretVariable
        };
        Assert.HasCount(1, await Analyze(detector, model));
        Assert.AreEqual("first-fixture-value", handler.Headers.Single());
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
    public async Task LegacyProfileSelectionFailsWithMigrationInstructions()
    {
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        var model = Workspace(selectProfile: false);
        model.NinjaConfig.HttpProfiles = ["source"];
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, model));
        Assert.Contains("http.profiles", error.Message);
        Assert.IsEmpty(handler.Headers);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("https://other.example")]
    [DataRow("https://source.example:444")]
    [DataRow("http://source.example")]
    [DataRow("https://source.example/path")]
    public async Task MissingOrDifferentCredentialOriginFailsBeforeDownloading(string? origin)
    {
        Environment.SetEnvironmentVariable(SecretVariable + "_ORIGIN", origin);
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, Workspace()));
        Assert.IsEmpty(handler.Headers);
    }

    [TestMethod]
    public async Task RepositoryCannotRedirectCredentialByChangingProfileAndUrl()
    {
        Configure("https://other.example");
        using var handler = new FileHandler();
        using var client = new HttpClient(handler);
        var detector = new ExpectFilesDetector(client, NullLogger<ExpectFilesDetector>.Instance, client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Analyze(detector, Workspace("https://other.example/file")));
        Assert.IsEmpty(handler.Headers);
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
    public async Task ExtractorReadsHttpRulesFromYaml()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "ninja.yaml"), """
            configVersion: 1
            http:
              profiles:
                source:
                  origin: https://source.example
                  headersFromEnv:
                    X-Source-Key: NINJA_HTTP_TEST_SECRET
            files:
              - name: config.txt
                contentUri: https://source.example/files/config
            """);
        var model = await new Extractor(NullLogger<Extractor>.Instance).Parse(_root);
        Assert.IsEmpty(model.NinjaConfig.HttpProfiles);
        var profile = model.NinjaConfig.Http!.Profiles["source"]!;
        Assert.AreEqual("https://source.example", profile.Origin);
        Assert.AreEqual(SecretVariable, profile.HeadersFromEnv["X-Source-Key"]);
        Assert.AreEqual("config.txt", model.NinjaConfig.Files.Single().Name);
    }

    private sealed class FileHandler(HttpStatusCode status = HttpStatusCode.OK, string headerName = "X-Source-Key") : HttpMessageHandler
    {
        public List<string?> Headers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Headers.Add(request.Headers.TryGetValues(headerName, out var values) ? values.Single() : null);
            var response = new HttpResponseMessage(status) { Content = new StringContent("expected file") };
            if (status == HttpStatusCode.Found)
            {
                response.Headers.Location = new Uri("https://other.example/sign-in");
            }
            return Task.FromResult(response);
        }
    }
}
