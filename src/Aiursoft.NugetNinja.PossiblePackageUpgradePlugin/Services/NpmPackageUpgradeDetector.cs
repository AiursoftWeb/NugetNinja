using System.ComponentModel;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;

public class NpmPackageUpgradeDetector(IOptions<AppSettings> options) : IActionDetector
{
    public NpmPackageUpgradeDetector() : this(Options.Create(new AppSettings()))
    {
    }

    private static readonly string[] Sections = ["dependencies", "devDependencies", "optionalDependencies"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed record Dependency(string Section, string Name, string Prefix, NpmVersion? Floor,
        string? PinnedPrerelease = null);

    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        if (context.NinjaConfig.Npm?.UpdateDependencies != true) yield break;

        var directories = FindPackageDirectories(context.RootPath).ToArray();

        // Finish analysis of every independent package before exposing any modifying action.
        var actions = new List<IAction>();
        foreach (var directory in directories)
        {
            try
            {
                var action = await AnalyzePackageAsync(context, directory);
                if (action != null) actions.Add(action);
            }
            catch (Exception exception) when (exception is InvalidOperationException or IOException or InvalidDataException or
                                            NotSupportedException or JsonException or TimeoutException)
            {
                var message = $"{Path.GetRelativePath(context.RootPath, directory)}/package.json: {exception.Message}";
                throw exception switch
                {
                    NotSupportedException => new NotSupportedException(message, exception),
                    InvalidOperationException => new InvalidOperationException(message, exception),
                    TimeoutException => new TimeoutException(message, exception),
                    _ => new InvalidDataException(message, exception)
                };
            }
        }

        foreach (var action in actions) yield return action;
    }

    private static IEnumerable<string> FindPackageDirectories(string directory)
    {
        var manifest = Path.Combine(directory, "package.json");
        var packageLock = Path.Combine(directory, "package-lock.json");
        if (File.Exists(manifest) || File.Exists(packageLock) || File.Exists(Path.Combine(directory, "npm-shrinkwrap.json")))
            yield return directory;

        foreach (var child in new DirectoryInfo(directory).EnumerateDirectories().OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            if ((child.Attributes & FileAttributes.ReparsePoint) != 0 ||
                child.Name is ".git" or "node_modules" or "bin" or "obj") continue;
            foreach (var package in FindPackageDirectories(child.FullName)) yield return package;
        }
    }

    private async Task<IAction?> AnalyzePackageAsync(Model context, string directory)
    {
        var manifestPath = Path.Combine(directory, "package.json");
        var lockPath = Path.Combine(directory, "package-lock.json");
        if (File.Exists(Path.Combine(directory, "npm-shrinkwrap.json")))
            throw new NotSupportedException("npm updates do not support shrinkwrap.");
        if (!File.Exists(manifestPath) || !File.Exists(lockPath))
            throw new InvalidDataException("npm updates require both package.json and package-lock.json.");
        if ((File.GetAttributes(manifestPath) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(lockPath) & FileAttributes.ReparsePoint) != 0)
            throw new NotSupportedException("npm updates do not support linked manifests or lockfiles.");

        var originalManifest = await File.ReadAllTextAsync(manifestPath);
        var originalLock = await File.ReadAllTextAsync(lockPath);
        var manifest = JsonNode.Parse(originalManifest) as JsonObject
            ?? throw new InvalidDataException("The npm manifest must be a JSON object.");
        var packageLock = JsonNode.Parse(originalLock) as JsonObject
            ?? throw new InvalidDataException("The npm lockfile must be a JSON object.");
        if (manifest.ContainsKey("workspaces"))
        {
            throw new NotSupportedException("npm updates require independent packages without workspaces.");
        }

        var version = Path.GetRelativePath(context.RootPath, directory) == "."
            ? NpmVersion.Parse(manifest["version"]?.GetValue<string>()
                ?? throw new InvalidDataException("npm updates require a root package version."))
            : null;
        if (packageLock["lockfileVersion"]?.GetValue<int>() is not (2 or 3))
            throw new NotSupportedException("npm updates require package-lock.json format 2 or 3.");
        var packages = packageLock["packages"]?.AsObject()
            ?? throw new NotSupportedException("npm updates require package-lock.json format 2 or 3.");
        if (packages[""] is not JsonObject)
            throw new InvalidDataException("The npm lockfile has no root package.");
        var dependencies = new List<Dependency>();
        var resolutionManifest = (JsonObject)manifest.DeepClone();
        foreach (var section in Sections)
        {
            if (manifest[section] is not JsonObject references) continue;
            foreach (var (name, node) in references)
            {
                var spec = node!.GetValue<string>();
                var prefix = spec.StartsWith('^') || spec.StartsWith('~') ? spec[..1] : string.Empty;
                var locked = packages["node_modules/" + name]?["version"]?.GetValue<string>()
                    ?? throw new InvalidDataException($"The npm lockfile has no installed version for '{name}'.");
                if (NpmVersion.IsExactPrerelease(spec))
                {
                    if (locked != spec)
                        throw new InvalidDataException($"The locked prerelease of '{name}' must match its exact declaration.");
                    dependencies.Add(new Dependency(section, name, string.Empty, null, spec));
                    continue;
                }

                var declared = NpmVersion.Parse(spec[prefix.Length..]);
                var floor = NpmVersion.Parse(locked);
                if (!declared.Allows(floor))
                {
                    throw new InvalidDataException($"The locked version of '{name}' is outside its compatible update line.");
                }

                dependencies.Add(new Dependency(section, name, prefix, floor));
                resolutionManifest[section]![name] = floor.UpdateRange;
            }
        }

        if (dependencies.Count == 0) return null;

        var temporary = Directory.CreateTempSubdirectory("nugetninja-npm-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(temporary, "package.json"), Serialize(resolutionManifest));
            await File.WriteAllTextAsync(Path.Combine(temporary, "package-lock.json"), originalLock);
            var npmrc = Path.Combine(directory, ".npmrc");
            if (!File.Exists(npmrc)) npmrc = Path.Combine(context.RootPath, ".npmrc");
            if (File.Exists(npmrc)) File.Copy(npmrc, Path.Combine(temporary, ".npmrc"));
            var contributorRegistries = await ResolveAsync(temporary, dependencies.DistinctBy(d => d.Name).ToArray());

            var resolvedLock = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(temporary, "package-lock.json")))!.AsObject();
            if (contributorRegistries != null)
                RestoreContributorSources(resolvedLock, contributorRegistries);
            var resolvedPackages = resolvedLock["packages"]!.AsObject();
            var resolvedRoot = resolvedPackages[""]!.AsObject();
            var updated = (JsonObject)manifest.DeepClone();
            var changes = new List<string>();
            foreach (var dependency in dependencies)
            {
                var resolvedVersion = resolvedPackages["node_modules/" + dependency.Name]!["version"]!.GetValue<string>();
                if (dependency.PinnedPrerelease != null)
                {
                    if (resolvedVersion != dependency.PinnedPrerelease)
                        throw new InvalidDataException($"npm changed the fixed prerelease of '{dependency.Name}'.");
                    continue;
                }

                var resolved = NpmVersion.Parse(resolvedVersion);
                var floor = dependency.Floor!;
                if (!floor.Allows(resolved))
                {
                    throw new InvalidDataException($"npm selected an incompatible or downgraded version for '{dependency.Name}'.");
                }

                updated[dependency.Section]![dependency.Name] = dependency.Prefix + resolved;
                if (floor != resolved) changes.Add($"{dependency.Name}: {floor} -> {resolved}");
            }

            // Restore the original declaration style in both files. npm only resolves the bounded candidate graph.
            foreach (var section in Sections)
            {
                if (updated[section] is { } references) resolvedRoot[section] = references.DeepClone();
                else resolvedRoot.Remove(section);
            }

            if (JsonNode.DeepEquals(manifest, updated) && JsonNode.DeepEquals(packageLock, resolvedLock)) return null;

            var details = changes.Count == 0 ? "Refresh the npm dependency declarations and lockfile" : string.Join("; ", changes);
            if (version != null)
            {
                // Root packages are the existing npm publishers. Nested web assets are not published independently.
                var next = version.NextPatch().ToString();
                updated["version"] = next;
                resolvedLock["version"] = next;
                resolvedRoot["version"] = next;
                details += $". Package version: {version} -> {next}.";
            }

            var owner = context.AllProjects
                .Where(project => IsWithin(Path.GetDirectoryName(project.PathOnDisk)!, directory))
                .OrderByDescending(project => Path.GetDirectoryName(project.PathOnDisk)!.Length)
                .FirstOrDefault();
            return new NpmPackageUpgrade(directory, originalManifest, originalLock,
                Serialize(updated), Serialize(resolvedLock),
                $"{Path.GetRelativePath(context.RootPath, manifestPath)}: {details}", owner);
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    private static bool IsWithin(string parent, string directory)
    {
        var relative = Path.GetRelativePath(parent, directory);
        return !Path.IsPathRooted(relative) && relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string Serialize(JsonNode node) => node.ToJsonString(JsonOptions) + "\n";

    private async Task<IReadOnlyDictionary<string, Uri>?> ResolveAsync(string directory, Dependency[] dependencies)
    {
        var arguments = new List<string> { "update" };
        var registry = options.Value.NpmRegistry;
        Dictionary<string, Uri>? contributorRegistries = null;
        if (!string.IsNullOrWhiteSpace(registry))
        {
            var uri = new Uri(registry, UriKind.Absolute);
            if ((uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) || uri.UserInfo.Length > 0)
                throw new InvalidOperationException("The npm registry must use HTTPS, or loopback HTTP, without embedded credentials.");
            arguments.Add($"--registry={registry}");
            // Include scopes configured for transitive dependencies, not just direct references.
            var config = JsonNode.Parse(await RunNpmAsync(directory, ["config", "list", "--json"]))?.AsObject()
                ?? throw new InvalidDataException("npm returned invalid registry configuration.");
            // Machine registry overrides select where the bot resolves packages, not the
            // addresses committed for contributors. Let npm interpret the project configuration.
            var contributorConfig = Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(IsRegistryEnvironment)
                ? JsonNode.Parse(await RunNpmAsync(directory, ["config", "list", "--json"], ignoreRegistryEnvironment: true))!.AsObject()
                : config;
            contributorRegistries = contributorConfig
                .Where(item => item.Key == "registry" || item.Key.EndsWith(":registry", StringComparison.Ordinal))
                .ToDictionary(item => item.Key, item => new Uri(item.Value!.GetValue<string>().TrimEnd('/') + "/"), StringComparer.Ordinal);
            if (!contributorRegistries.ContainsKey("registry") || contributorRegistries.Values.Any(source => source.UserInfo.Length > 0))
                throw new InvalidDataException("Registry URLs with embedded credentials cannot be committed to lockfiles; configure npm authentication separately.");
            var scopes = config.Select(item => item.Key)
                .Where(key => Regex.IsMatch(key, @"^@[a-z0-9_.-]+:registry$", RegexOptions.CultureInvariant))
                .Select(key => key.Split(':')[0])
                .Concat(dependencies.Where(d => d.Name.StartsWith('@')).Select(d => d.Name.Split('/')[0]))
                .Distinct(StringComparer.Ordinal);
            foreach (var scope in scopes) arguments.Add($"--{scope}:registry={registry}");
        }
        foreach (var dependency in dependencies)
        {
            // Validate the manifest names before asking npm to refresh the bounded graph.
            var name = dependency.Name;
            if (!Regex.IsMatch(name, @"^(?:@[a-z0-9_.-]+/)?[a-z0-9_][a-z0-9_.-]*$", RegexOptions.CultureInvariant))
                throw new InvalidDataException($"Unsupported npm package name '{name}'.");
        }

        foreach (var option in new[] { "--package-lock-only", "--ignore-scripts", "--no-audit", "--no-fund", "--save", "--save-exact", "--include=dev" })
            arguments.Add(option);

        await RunNpmAsync(directory, arguments);
        return contributorRegistries;
    }

    private void RestoreContributorSources(JsonObject packageLock, IReadOnlyDictionary<string, Uri> registries)
    {
        var selected = new Uri(options.Value.NpmRegistry.TrimEnd('/') + "/");
        foreach (var (path, entry) in packageLock["packages"]!.AsObject())
        {
            var segment = path.LastIndexOf("node_modules/", StringComparison.Ordinal);
            if (segment >= 0 && entry is JsonObject package)
                RestoreResolved(package, package["name"]?.GetValue<string>() ?? path[(segment + "node_modules/".Length)..]);
        }
        if (packageLock["dependencies"] is JsonObject legacyDependencies)
            RestoreLegacy(legacyDependencies);

        void RestoreLegacy(JsonObject dependencies)
        {
            foreach (var (name, entry) in dependencies)
            {
                if (entry is not JsonObject package) continue;
                RestoreResolved(package, name);
                if (package["dependencies"] is JsonObject children) RestoreLegacy(children);
            }
        }

        void RestoreResolved(JsonObject package, string name)
        {
            if (!Uri.TryCreate(package["resolved"]?.GetValue<string>(), UriKind.Absolute, out var resolved) ||
                !selected.IsBaseOf(resolved)) return;
            var scope = name.StartsWith('@') && name.Contains('/') ? name[..name.IndexOf('/')] + ":registry" : "registry";
            var contributor = registries.GetValueOrDefault(scope) ?? registries["registry"];
            package["resolved"] = new Uri(contributor, selected.MakeRelativeUri(resolved)).AbsoluteUri;
        }
    }

    private static bool IsRegistryEnvironment(string name) =>
        name.StartsWith("npm_config_", StringComparison.OrdinalIgnoreCase) && name.EndsWith("registry", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> RunNpmAsync(string directory, IEnumerable<string> arguments, bool ignoreRegistryEnvironment = false)
    {
        var start = new ProcessStartInfo("npm")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        if (ignoreRegistryEnvironment)
            foreach (var name in start.Environment.Keys.Where(IsRegistryEnvironment).ToArray()) start.Environment.Remove(name);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process();
        process.StartInfo = start;
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("npm updates require Node.js and npm on PATH.", exception);
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("npm dependency resolution exceeded three minutes.");
        }

        var stdout = await output;
        var stderr = await error;
        if (process.ExitCode != 0)
        {
            var code = Regex.Match(stderr, @"npm (?:error|ERR!) code ([A-Z0-9_]+)").Groups[1].Value;
            throw new InvalidOperationException($"npm dependency resolution failed (exit {process.ExitCode}, code '{code}'). No project files were changed; check package compatibility and registry access.");
        }
        return stdout;
    }
}
