using System.ComponentModel;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Models;

namespace Aiursoft.NugetNinja.PossiblePackageUpgradePlugin.Services;

public class NpmPackageUpgradeDetector : IActionDetector
{
    private static readonly string[] Sections = ["dependencies", "devDependencies", "optionalDependencies"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private sealed record Dependency(string Section, string Name, string Prefix, NpmVersion Floor);

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

    private static async Task<IAction?> AnalyzePackageAsync(Model context, string directory)
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
                var declared = NpmVersion.Parse(spec[prefix.Length..]);
                var locked = packages["node_modules/" + name]?["version"]?.GetValue<string>()
                    ?? throw new InvalidDataException($"The npm lockfile has no installed version for '{name}'.");
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
            await ResolveAsync(temporary, dependencies.DistinctBy(d => d.Name).ToArray());

            var resolvedLock = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(temporary, "package-lock.json")))!.AsObject();
            var resolvedPackages = resolvedLock["packages"]!.AsObject();
            var resolvedRoot = resolvedPackages[""]!.AsObject();
            var updated = (JsonObject)manifest.DeepClone();
            var changes = new List<string>();
            foreach (var dependency in dependencies)
            {
                var resolved = NpmVersion.Parse(resolvedPackages["node_modules/" + dependency.Name]!["version"]!.GetValue<string>());
                if (!dependency.Floor.Allows(resolved))
                {
                    throw new InvalidDataException($"npm selected an incompatible or downgraded version for '{dependency.Name}'.");
                }

                updated[dependency.Section]![dependency.Name] = dependency.Prefix + resolved;
                if (dependency.Floor != resolved) changes.Add($"{dependency.Name}: {dependency.Floor} -> {resolved}");
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

    private static async Task ResolveAsync(string directory, Dependency[] dependencies)
    {
        var start = new ProcessStartInfo("npm")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("install");
        foreach (var dependency in dependencies)
        {
            // Package names come from a manifest, never from a shell command string or npm option.
            var name = dependency.Name;
            if (!Regex.IsMatch(name, @"^(?:@[a-z0-9_.-]+/)?[a-z0-9_][a-z0-9_.-]*$", RegexOptions.CultureInvariant))
                throw new InvalidDataException($"Unsupported npm package name '{name}'.");
            start.ArgumentList.Add(name + "@" + dependency.Floor.UpdateRange);
        }

        foreach (var option in new[] { "--package-lock-only", "--ignore-scripts", "--no-audit", "--no-fund", "--save", "--save-exact", "--include=dev" })
            start.ArgumentList.Add(option);

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

        await output;
        var stderr = await error;
        if (process.ExitCode != 0)
        {
            var code = Regex.Match(stderr, @"npm (?:error|ERR!) code ([A-Z0-9_]+)").Groups[1].Value;
            throw new InvalidOperationException($"npm dependency resolution failed (exit {process.ExitCode}, code '{code}'). No project files were changed; check package compatibility and registry access.");
        }
    }
}
