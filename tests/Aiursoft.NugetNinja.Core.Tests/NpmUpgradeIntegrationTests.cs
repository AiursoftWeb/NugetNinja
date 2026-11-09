using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Aiursoft.Canon;
using Aiursoft.NugetNinja.AllOfficialsPlugin.Services;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.Core.Services.Nuget;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;
using Microsoft.Extensions.DependencyInjection;
using WorkspaceModel = Aiursoft.NugetNinja.Core.Model.Workspace.Model;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class NpmUpgradeIntegrationTests
{
    [TestMethod]
    [DataRow(false, true, true, false, DisplayName = "Shared opt-in upgrades packages in the same run")]
    [DataRow(true, false, true, false, DisplayName = "Shared opt-out prevents upgrades in the same run")]
    [DataRow(false, true, false, false, DisplayName = "Preview leaves configuration and packages untouched")]
    [DataRow(false, true, true, true, DisplayName = "Update-only mode does not synchronize configuration")]
    public async Task OfficialWorkflowUsesSynchronizedNpmConfiguration(
        bool initiallyEnabled, bool remotelyEnabled, bool apply, bool updateOnly)
    {
        await using var fixture = new RegistryFixture();
        await fixture.CreateLockAsync(3);
        fixture.PublishUpdates = true;
        var configPath = Path.Combine(fixture.DirectoryPath, "ninja.yaml");
        static string Config(bool enabled) => $$"""
            configVersion: 1
            npm:
              updateDependencies: {{enabled.ToString().ToLowerInvariant()}}
            files:
              - name: ninja.yaml
                contentUri: https://config.test/ninja.yaml
            """;
        var originalConfig = Config(initiallyEnabled);
        await File.WriteAllTextAsync(configPath, originalConfig);
        var originalPackages = await fixture.ReadFilesAsync();
        using var handler = new SharedConfigurationHandler(Config(remotelyEnabled));
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddTaskCanon();
        services.AddLogging();
        services.Configure<AppSettings>(_ => { });
        services.AddSingleton(new HttpClient(handler));
        services.AddTransient<Extractor>();
        services.AddTransient<ProjectsEnumerator>();
        services.AddTransient<VersionCrossChecker>();
        services.AddTransient<NugetService>();
        new AllOfficialsPlugin.StartUp().ConfigureServices(services);
        services.AddTransient<RunAllOfficialPluginsService>();
        using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<RunAllOfficialPluginsService>();

        await runner.RunAllPlugins(fixture.DirectoryPath, apply, updateOnly);

        Assert.AreEqual(apply && !updateOnly ? Config(remotelyEnabled) : originalConfig,
            await File.ReadAllTextAsync(configPath));
        var shouldUpgrade = apply && !updateOnly && remotelyEnabled;
        var manifest = JsonNode.Parse((await fixture.ReadFilesAsync())[0])!;
        Assert.AreEqual(shouldUpgrade ? "1.2.0" : "1.0.0",
            manifest["dependencies"]!["stable"]!.GetValue<string>());
        Assert.AreEqual(shouldUpgrade ? "1.0.1" : "1.0.0", manifest["version"]!.GetValue<string>());
        if (!shouldUpgrade)
            CollectionAssert.AreEqual(originalPackages, await fixture.ReadFilesAsync());
        Assert.AreEqual(updateOnly ? 0 : 1, handler.Requests);
        if (apply && !updateOnly)
        {
            var updatedPackages = await fixture.ReadFilesAsync();
            await runner.RunAllPlugins(fixture.DirectoryPath, apply, updateOnly);
            CollectionAssert.AreEqual(updatedPackages, await fixture.ReadFilesAsync(),
                "A second official run must not introduce another package or version change.");
        }
        Assert.IsFalse(File.Exists(Path.Combine(fixture.DirectoryPath, "script-ran")));
        Assert.AreEqual(0, fixture.TarballRequests);
    }

    private sealed class SharedConfigurationHandler(string content) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual("https://config.test/ninja.yaml", request.RequestUri!.AbsoluteUri);
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task NativeResolutionPreservesBoundariesAndConverges(int lockVersion)
    {
        await using var fixture = new RegistryFixture();
        await fixture.CreateLockAsync(lockVersion);
        var before = await fixture.ReadFilesAsync();
        fixture.PublishUpdates = true;
        var actions = await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync();
        Assert.HasCount(1, actions);
        CollectionAssert.AreEqual(before, await fixture.ReadFilesAsync(), "Analysis must not write project files.");
        await actions[0].TakeActionAsync();
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(fixture.ManifestPath))!;
        var packageLock = JsonNode.Parse(await File.ReadAllTextAsync(fixture.LockPath))!;
        Assert.AreEqual("1.2.0", manifest["dependencies"]!["stable"]!.GetValue<string>());
        Assert.AreEqual("^0.4.2", manifest["dependencies"]!["zero"]!.GetValue<string>());
        Assert.AreEqual("~1.1.0", manifest["dependencies"]!["@scope/scoped"]!.GetValue<string>());
        Assert.AreEqual("2.1.0", manifest["devDependencies"]!["tool"]!.GetValue<string>());
        Assert.AreEqual("3.1.0", manifest["optionalDependencies"]!["optional"]!.GetValue<string>());
        Assert.AreEqual("1.0.1", manifest["version"]!.GetValue<string>());
        Assert.AreEqual("1.0.1", packageLock["version"]!.GetValue<string>());
        Assert.AreEqual("1.0.1", packageLock["packages"]![""]!["version"]!.GetValue<string>());
        foreach (var section in new[] { "dependencies", "devDependencies", "optionalDependencies" })
            Assert.IsTrue(JsonNode.DeepEquals(manifest[section], packageLock["packages"]![""]![section]));
        var updated = await fixture.ReadFilesAsync();
        Assert.HasCount(0, await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync());
        CollectionAssert.AreEqual(updated, await fixture.ReadFilesAsync(), "A second run must not bump the version again.");
        Assert.IsFalse(File.Exists(Path.Combine(fixture.DirectoryPath, "script-ran")));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.DirectoryPath, "node_modules")));
        Assert.AreEqual(0, fixture.TarballRequests, "Lock-only resolution must not fetch package archives.");
    }

    [TestMethod]
    [DataRow("offline")]
    [DataRow("peer-conflict")]
    public async Task FailedResolutionLeavesBothOriginalFilesUnchanged(string failure)
    {
        await using var fixture = new RegistryFixture();
        await fixture.CreateLockAsync(3);
        var before = await fixture.ReadFilesAsync();
        fixture.PublishUpdates = true;
        fixture.Failure = failure;
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync());
        StringAssert.Contains(exception.Message, "npm dependency resolution failed");
        CollectionAssert.AreEqual(before, await fixture.ReadFilesAsync());
    }

    [TestMethod]
    public async Task UnchangedDirectReferencesRetainLockedTransitiveVersions()
    {
        await using var fixture = new RegistryFixture();
        fixture.IncludeTransitive = true;
        await fixture.CreateLockAsync(3);
        fixture.PublishUpdates = true;
        var before = await fixture.ReadFilesAsync();
        Assert.HasCount(0, await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync());
        CollectionAssert.AreEqual(before, await fixture.ReadFilesAsync());
        var packageLock = JsonNode.Parse(before[1])!;
        Assert.AreEqual("1.0.0", packageLock["packages"]!["node_modules/leaf"]!["version"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task ApplyDoesNotOverwriteChangesMadeAfterAnalysis()
    {
        await using var fixture = new RegistryFixture();
        await fixture.CreateLockAsync(3);
        fixture.PublishUpdates = true;
        var actions = await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync();
        await File.AppendAllTextAsync(fixture.LockPath, "\n");
        var changed = await fixture.ReadFilesAsync();
        await Assert.ThrowsExactlyAsync<IOException>(() => actions.Single().TakeActionAsync());
        CollectionAssert.AreEqual(changed, await fixture.ReadFilesAsync());
    }

    [TestMethod]
    public async Task DependencyReleasesUpdateConsumerAndItsTransitiveLockOncePerRelease()
    {
        await using var fixture = new RegistryFixture();
        fixture.IncludeTransitive = true;
        await fixture.CreateLockAsync(3);
        foreach (var patch in new[] { 1, 2 })
        {
            fixture.SetAvailableVersions("stable", $"1.0.{patch}");
            fixture.SetAvailableVersions("leaf", $"1.{patch}.0");
            var before = await fixture.ReadFilesAsync();
            var actions = await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync();
            Assert.HasCount(1, actions);
            CollectionAssert.AreEqual(before, await fixture.ReadFilesAsync());
            await actions.Single().TakeActionAsync();
            var files = await fixture.ReadFilesAsync();
            var manifest = JsonNode.Parse(files[0])!;
            var packageLock = JsonNode.Parse(files[1])!;
            Assert.AreEqual($"1.0.{patch}", manifest["version"]!.GetValue<string>());
            Assert.AreEqual($"1.0.{patch}", manifest["dependencies"]!["stable"]!.GetValue<string>());
            Assert.AreEqual($"1.0.{patch}", packageLock["packages"]!["node_modules/stable"]!["version"]!.GetValue<string>());
            Assert.AreEqual($"1.{patch}.0", packageLock["packages"]!["node_modules/leaf"]!["version"]!.GetValue<string>());
            Assert.HasCount(0, await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync());
            CollectionAssert.AreEqual(files, await fixture.ReadFilesAsync());
        }
    }

    [TestMethod]
    public async Task StaleRegistryCannotDowngradeAnAlreadyUpdatedConsumer()
    {
        await using var fixture = new RegistryFixture();
        await fixture.CreateLockAsync(3);
        fixture.PublishUpdates = true;
        var actions = await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync();
        await actions.Single().TakeActionAsync();
        var before = await fixture.ReadFilesAsync();
        fixture.PublishUpdates = false;
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync());
        StringAssert.Contains(exception.Message, "npm dependency resolution failed");
        CollectionAssert.AreEqual(before, await fixture.ReadFilesAsync());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MultipleNestedPackagesResolveIndependentlyAndConvergeWithoutPublishingVersions(bool versionless)
    {
        var root = Directory.CreateTempSubdirectory("ninja-web-test-").FullName;
        await using var first = new RegistryFixture();
        await using var second = new RegistryFixture();
        try
        {
            await first.CreateLockAsync(3);
            await second.CreateLockAsync(2);
            first.MoveTo(Path.Combine(root, "src", "First", "wwwroot"));
            second.MoveTo(Path.Combine(root, "src", "Second", "wwwroot"));
            if (versionless)
            {
                var manifest = JsonNode.Parse(await File.ReadAllTextAsync(first.ManifestPath))!.AsObject();
                manifest.Remove("version");
                manifest["private"] = true;
                await File.WriteAllTextAsync(first.ManifestPath, manifest.ToJsonString());
                var packageLock = JsonNode.Parse(await File.ReadAllTextAsync(first.LockPath))!.AsObject();
                packageLock.Remove("version");
                packageLock["packages"]![""]!.AsObject().Remove("version");
                await File.WriteAllTextAsync(first.LockPath, packageLock.ToJsonString());
            }
            first.PublishUpdates = second.PublishUpdates = true;
            second.SetAvailableVersions("stable", "1.0.0", "1.4.0", "2.0.0");
            var model = first.Model;
            model.RootPath = root;
            var actions = await new NpmPackageUpgradeDetector().AnalyzeAsync(model).ToListAsync();
            Assert.HasCount(2, actions);
            foreach (var action in actions) await action.TakeActionAsync();
            Assert.AreEqual("1.2.0", JsonNode.Parse((await first.ReadFilesAsync())[0])!["dependencies"]!["stable"]!.GetValue<string>());
            Assert.AreEqual("1.4.0", JsonNode.Parse((await second.ReadFilesAsync())[0])!["dependencies"]!["stable"]!.GetValue<string>());
            foreach (var fixture in new[] { first, second })
            {
                var files = await fixture.ReadFilesAsync();
                var expectedVersion = fixture == first && versionless ? null : "1.0.0";
                Assert.AreEqual(expectedVersion, JsonNode.Parse(files[0])!["version"]?.GetValue<string>());
                Assert.AreEqual(expectedVersion, JsonNode.Parse(files[1])!["version"]?.GetValue<string>());
                Assert.AreEqual(0, fixture.TarballRequests);
            }
            Assert.HasCount(0, await new NpmPackageUpgradeDetector().AnalyzeAsync(model).ToListAsync());
        }
        finally
        {
            // Fixtures remove their individual package directories after the test.
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public async Task RootAndNestedPackagesKeepDistinctVersionAndProjectOwnership()
    {
        await using var root = new RegistryFixture();
        await using var nested = new RegistryFixture();
        await root.CreateLockAsync(3);
        await nested.CreateLockAsync(3);
        nested.MoveTo(Path.Combine(root.DirectoryPath, "src", "Web", "wwwroot"));
        root.PublishUpdates = nested.PublishUpdates = true;
        var model = root.Model;
        var project = new Aiursoft.NugetNinja.Core.Model.Workspace.Project(
            Path.Combine(root.DirectoryPath, "src", "Web", "Web.csproj"), new HtmlAgilityPack.HtmlDocument().DocumentNode);
        model.AllProjects.Add(project);
        var actions = await new NpmPackageUpgradeDetector().AnalyzeAsync(model).ToListAsync();
        Assert.HasCount(2, actions);
        Assert.IsNull(actions[0].SourceProject);
        Assert.AreSame(project, actions[1].SourceProject);
        foreach (var action in actions) await action.TakeActionAsync();
        Assert.AreEqual("1.0.1", JsonNode.Parse((await root.ReadFilesAsync())[0])!["version"]!.GetValue<string>());
        Assert.AreEqual("1.0.0", JsonNode.Parse((await nested.ReadFilesAsync())[0])!["version"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task NestedPackageWithoutConfigUsesRepositoryRegistry()
    {
        await using var root = new RegistryFixture();
        await using var nested = new RegistryFixture();
        await root.CreateLockAsync(3);
        await nested.CreateLockAsync(3);
        nested.MoveTo(Path.Combine(root.DirectoryPath, "src", "Web", "wwwroot"));
        File.Delete(Path.Combine(nested.DirectoryPath, ".npmrc"));
        root.PublishUpdates = true;
        nested.PublishUpdates = true;
        nested.SetAvailableVersions("stable", "1.0.0", "1.4.0");
        var actions = await new NpmPackageUpgradeDetector().AnalyzeAsync(root.Model).ToListAsync();
        foreach (var action in actions) await action.TakeActionAsync();
        var manifest = JsonNode.Parse((await nested.ReadFilesAsync())[0])!;
        Assert.AreEqual("1.2.0", manifest["dependencies"]!["stable"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task AnInvalidLaterPackageDoesNotExposePartialActions()
    {
        await using var fixture = new RegistryFixture();
        await fixture.CreateLockAsync(3);
        fixture.PublishUpdates = true;
        var before = await fixture.ReadFilesAsync();
        var bad = Path.Combine(fixture.DirectoryPath, "src", "Bad", "wwwroot");
        Directory.CreateDirectory(bad);
        await File.WriteAllTextAsync(Path.Combine(bad, "package.json"), "{}");
        var count = 0;
        var exception = await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
        {
            await foreach (var action in new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model))
            {
                count++;
                await action.TakeActionAsync();
            }
        });
        Assert.AreEqual(0, count);
        StringAssert.Contains(exception.Message, "src");
        StringAssert.Contains(exception.Message, "Bad");
        CollectionAssert.AreEqual(before, await fixture.ReadFilesAsync());
    }

    [TestMethod]
    public async Task GeneratedDirectoriesAndLinksCannotIntroducePackages()
    {
        await using var fixture = new RegistryFixture();
        await using var outside = new RegistryFixture();
        await fixture.CreateLockAsync(3);
        foreach (var name in new[] { "node_modules", "bin", "obj", ".git" })
        {
            var directory = Path.Combine(fixture.DirectoryPath, name, "broken");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "package.json"), "{}");
        }
        await File.WriteAllTextAsync(Path.Combine(outside.DirectoryPath, "package.json"), "{}");
        if (!OperatingSystem.IsWindows())
            Directory.CreateSymbolicLink(Path.Combine(fixture.DirectoryPath, "linked"), outside.DirectoryPath);
        fixture.PublishUpdates = true;
        Assert.HasCount(1, await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync());
    }

    [TestMethod]
    [DataRow("workspace")]
    [DataRow("shrinkwrap")]
    [DataRow("lock-only")]
    [DataRow("malformed")]
    [DataRow("null")]
    public async Task UnsupportedNestedPackagesFailWithTheirPath(string kind)
    {
        await using var fixture = new RegistryFixture();
        await fixture.CreateLockAsync(3);
        var child = Path.Combine(fixture.DirectoryPath, "src", "Broken", "wwwroot");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(child, "package.json"),
            kind == "workspace" ? "{\"workspaces\":[]}" : kind == "malformed" ? "broken json" : kind == "null" ? "null" : "{}");
        await File.WriteAllTextAsync(Path.Combine(child, "package-lock.json"), "{}");
        if (kind == "shrinkwrap") await File.WriteAllTextAsync(Path.Combine(child, "npm-shrinkwrap.json"), "{}");
        if (kind == "lock-only") File.Delete(Path.Combine(child, "package.json"));
        Exception? caught = null;
        try { await new NpmPackageUpgradeDetector().AnalyzeAsync(fixture.Model).ToListAsync(); }
        catch (Exception exception) { caught = exception; }
        Assert.IsNotNull(caught);
        StringAssert.Contains(caught.Message, "Broken");
        StringAssert.Contains(caught.Message, "package.json");
    }

    // A local metadata-only registry keeps these native npm regressions independent of public releases.
    private sealed class RegistryFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task _server;
        private bool _stopping;
        private readonly Dictionary<string, string[]> _versions = new()
        {
            ["stable"] = ["1.0.0", "1.2.0", "1.3.0-beta.1", "2.0.0"],
            ["zero"] = ["0.4.0", "0.4.2", "0.5.0", "1.0.0"],
            ["@scope/scoped"] = ["1.0.0", "1.1.0", "2.0.0"],
            ["tool"] = ["2.0.0", "2.1.0", "3.0.0"],
            ["optional"] = ["3.0.0", "3.1.0", "4.0.0"],
            ["leaf"] = ["1.0.0", "1.1.0", "2.0.0"]
        };
        private readonly string _origin;
        public string DirectoryPath { get; private set; } = Directory.CreateTempSubdirectory("ninja-npm-test-").FullName;
        public string ManifestPath => Path.Combine(DirectoryPath, "package.json");
        public string LockPath => Path.Combine(DirectoryPath, "package-lock.json");
        public bool PublishUpdates { get; set; }
        public bool IncludeTransitive { get; set; }
        public string? Failure { get; set; }
        public int TarballRequests { get; private set; }
        public WorkspaceModel Model => new()
        {
            RootPath = DirectoryPath,
            NinjaConfig = new NinjaConfig { Npm = new NinjaNpmConfig { UpdateDependencies = true } }
        };

        public RegistryFixture()
        {
            _listener.Start();
            _origin = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
            _server = ServeAsync();
        }

        public void MoveTo(string directory)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
            Directory.Move(DirectoryPath, directory);
            var npmrc = Path.Combine(directory, ".npmrc");
            File.WriteAllText(npmrc, File.ReadAllText(npmrc).Replace(DirectoryPath, directory));
            DirectoryPath = directory;
        }

        public void SetAvailableVersions(string name, params string[] versions) => _versions[name] = versions;

        public async Task CreateLockAsync(int lockVersion)
        {
            var marker = Path.Combine(DirectoryPath, "script-ran");
            var manifest = new JsonObject
            {
                ["name"] = "@aiursoft/ninja-fixture", ["version"] = "1.0.0",
                ["scripts"] = new JsonObject
                {
                    ["preinstall"] = $"touch {marker}", ["install"] = $"touch {marker}", ["prepare"] = $"touch {marker}"
                },
                ["dependencies"] = new JsonObject { ["stable"] = "1.0.0", ["zero"] = "^0.4.0", ["@scope/scoped"] = "~1.0.0" },
                ["devDependencies"] = new JsonObject { ["tool"] = "2.0.0" },
                ["optionalDependencies"] = new JsonObject { ["optional"] = "3.0.0" }
            };
            await File.WriteAllTextAsync(ManifestPath, manifest.ToJsonString());
            await File.WriteAllTextAsync(Path.Combine(DirectoryPath, ".npmrc"),
                $"registry={_origin}\nfetch-retries=0\ncache={DirectoryPath}/cache\n");
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("npm")
            {
                WorkingDirectory = DirectoryPath, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "install", "--package-lock-only", "--ignore-scripts", "--no-audit", "--no-fund", $"--lockfile-version={lockVersion}" })
                process.StartInfo.ArgumentList.Add(argument);
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }
            await stdout;
            Assert.AreEqual(0, process.ExitCode, await stderr);
        }

        public async Task<string[]> ReadFilesAsync() =>
            [await File.ReadAllTextAsync(ManifestPath), await File.ReadAllTextAsync(LockPath)];

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    await using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var request = await reader.ReadLineAsync();
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                    var name = Uri.UnescapeDataString(request!.Split(' ')[1].Split('?')[0].TrimStart('/'));
                    if (name.StartsWith("tarballs/", StringComparison.Ordinal)) TarballRequests++;
                    var available = _versions.GetValueOrDefault(name);
                    var status = Failure == "offline" ? 503 : available == null ? 404 : 200;
                    var versions = new JsonObject();
                    if (available != null)
                    {
                        foreach (var version in PublishUpdates && (!IncludeTransitive || name == "leaf") ? available : available.Take(1))
                        {
                            var entry = new JsonObject
                            {
                                ["name"] = name, ["version"] = version,
                                ["dist"] = new JsonObject { ["tarball"] = $"{_origin}/tarballs/{name.Replace('/', '-')}-{version}.tgz", ["shasum"] = new string('a', 40) }
                            };
                            if (IncludeTransitive && name == "stable")
                                entry["dependencies"] = new JsonObject { ["leaf"] = "^" + _versions["leaf"][0] };
                            if (Failure == "peer-conflict" && name == "stable" && version == "1.2.0")
                                entry["peerDependencies"] = new JsonObject { ["missing-peer"] = "^9.0.0" };
                            versions[version] = entry;
                        }
                    }
                    var body = Encoding.UTF8.GetBytes(new JsonObject
                    {
                        ["name"] = name, ["dist-tags"] = new JsonObject { ["latest"] = available?[^1] }, ["versions"] = versions
                    }.ToJsonString());
                    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Result\r\nContent-Type: application/json\r\nCache-Control: no-store\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header);
                    await stream.WriteAsync(body);
                }
            }
            catch (SocketException) when (_stopping) { }
            catch (ObjectDisposedException) when (_stopping) { }
        }

        public async ValueTask DisposeAsync()
        {
            _stopping = true;
            _listener.Stop();
            await _server;
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }
}
