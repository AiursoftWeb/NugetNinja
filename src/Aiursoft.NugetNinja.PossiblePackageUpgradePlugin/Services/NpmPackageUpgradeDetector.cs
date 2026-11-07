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

        var manifestPath = Path.Combine(context.RootPath, "package.json");
        var lockPath = Path.Combine(context.RootPath, "package-lock.json");
        if (!File.Exists(manifestPath) || !File.Exists(lockPath))
        {
            throw new InvalidDataException("npm updates require root package.json and package-lock.json files.");
        }

        var originalManifest = await File.ReadAllTextAsync(manifestPath);
        var originalLock = await File.ReadAllTextAsync(lockPath);
        var manifest = JsonNode.Parse(originalManifest)!.AsObject();
        var packageLock = JsonNode.Parse(originalLock)!.AsObject();
        if (manifest.ContainsKey("workspaces") || File.Exists(Path.Combine(context.RootPath, "npm-shrinkwrap.json")))
        {
            throw new NotSupportedException("npm updates currently support a single root package with package-lock.json, without workspaces or shrinkwrap.");
        }

        var version = NpmVersion.Parse(manifest["version"]?.GetValue<string>()
            ?? throw new InvalidDataException("npm updates require a root package version."));
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

        if (dependencies.Count == 0) yield break;

        var temporary = Directory.CreateTempSubdirectory("nugetninja-npm-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(temporary, "package.json"), Serialize(resolutionManifest));
            await File.WriteAllTextAsync(Path.Combine(temporary, "package-lock.json"), originalLock);
            var npmrc = Path.Combine(context.RootPath, ".npmrc");
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

            if (JsonNode.DeepEquals(manifest, updated) && JsonNode.DeepEquals(packageLock, resolvedLock)) yield break;

            var next = version.NextPatch().ToString();
            updated["version"] = next;
            resolvedLock["version"] = next;
            resolvedRoot["version"] = next;
            var details = changes.Count == 0 ? "Refresh the npm dependency declarations and lockfile" : string.Join("; ", changes);
            yield return new NpmPackageUpgrade(context.RootPath, originalManifest, originalLock,
                Serialize(updated), Serialize(resolvedLock), $"{details}. Package version: {version} -> {next}.");
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
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
