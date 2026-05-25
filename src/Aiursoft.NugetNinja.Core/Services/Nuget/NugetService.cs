using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Aiursoft.Canon;
using Aiursoft.NugetNinja.Core.Model.Framework;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.Core.Services.Analyser;
using Aiursoft.NugetNinja.Core.Services.Nuget.Models;
using Aiursoft.NugetNinja.Core.Services.Utils;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aiursoft.NugetNinja.Core.Services.Nuget;

public class NugetService
{
    private const string DefaultNugetServer = "https://api.nuget.org/v3/index.json";
    private const string NuGetSearchUrl = "https://azuresearch-ea.nuget.org/query";
    private const string NuGetVulnerabilityBaseUrl = "https://api.nuget.org/v3/vulnerabilities/vulnerability.base.json";
    private readonly string _customNugetServer = DefaultNugetServer;
    private readonly string _patToken;
    private readonly bool _allowPreview;
    private readonly bool _allowPackageVersionCrossMicrosoftRuntime;
    private readonly CacheService _cacheService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NugetService> _logger;
    private readonly VersionCrossChecker _versionCrossChecker;
    private Dictionary<string, List<VulnerabilityBaseEntry>>? _vulnerabilityCache;

    public NugetService(
        CacheService cacheService,
        HttpClient httpClient,
        ILogger<NugetService> logger,
        IOptions<AppSettings> options,
        VersionCrossChecker versionCrossChecker)
    {
        var optionsValue = options.Value;

        _cacheService = cacheService;
        _httpClient = httpClient;
        _logger = logger;
        _versionCrossChecker = versionCrossChecker;
        _allowPreview = optionsValue.AllowPreview;
        _patToken = optionsValue.PatToken;
        _allowPackageVersionCrossMicrosoftRuntime = optionsValue.AllowCross;
        if (!string.IsNullOrWhiteSpace(optionsValue.CustomNugetServer))
        {
            _customNugetServer = optionsValue.CustomNugetServer;
        }
    }

    public async Task<NugetVersion> GetLatestVersion(string packageName, string[] runtimes)
    {
        var allVersions = (await GetAllPublishedVersions(packageName) ?? throw new InvalidOperationException($"No version found with package: '{packageName}'."))
            .OrderByDescending(t => t)
            .ToList();
        var first5Versions = allVersions 
            .Take(5)
            .ToList(); // Only take latest 5 versions.

        
        if (_allowPackageVersionCrossMicrosoftRuntime)
        {
            return allVersions.First();
        }
        else
        {
            var likeMsRuntimeVersions = _versionCrossChecker.LikeRuntimeVersions(first5Versions);
            if (!likeMsRuntimeVersions)
            {
                return allVersions.First();
            }
            
            _logger.LogTrace("The package {PackageName}'s first 5 versions look like matching MS runtime versions",
                packageName);
            var latest = allVersions.FirstOrDefault(v =>
            {
                if (v.PrimaryVersion == null) return false;
                var versionString = $"{v.PrimaryVersion.Major}.{v.PrimaryVersion.Minor}";
                return runtimes.Any(r => r.Contains(versionString));
            });
            return latest != default ? latest : allVersions.First();
        }
    }

    public Task<CatalogInformation> GetPackageDeprecationInfo(Package package)
    {
        return _cacheService.RunWithCache($"nuget-deprecation-info-{package}-version-{package.Version}-cache",
            () => GetPackageDeprecationInfoFromNuget(package));
    }

    public Task<IReadOnlyCollection<NugetVersion>> GetAllPublishedVersions(string packageName)
    {
        return _cacheService.RunWithCache($"all-nuget-published-versions-package-{packageName}-preview-cache",
            () => GetAllPublishedVersionsFromNuget(packageName));
    }

    public Task<NugetServerEndPoints> GetApiEndpoint(string? overrideServer = null)
    {
        var server = overrideServer ?? _customNugetServer;
        return _cacheService.RunWithCache($"nuget-server-{server}-endpoint-cache",
            () => GetApiEndpointFromNuget(server));
    }

    public Task<Package[]> GetPackageDependencies(Package package)
    {
        return _cacheService.RunWithCache($"nuget-package-{package.Name}-dependencies-{package.Version}-cache",
            () => GetPackageDependenciesFromNuget(package));
    }

    private async Task<NugetServerEndPoints> GetApiEndpointFromNuget(string? overrideServer = null)
    {
        var serverRoot = overrideServer ?? _customNugetServer;
        if (serverRoot.EndsWith("/")) serverRoot = serverRoot.TrimEnd('/');
        if (!serverRoot.EndsWith("index.json"))
        {
            if (!serverRoot.EndsWith("/v3"))
            {
                serverRoot += "/v3";
            }
            serverRoot += "/index.json";
        }

        var responseModel = await HttpGetJson<NugetServerIndex>(serverRoot, _patToken);
        var packageBaseAddress = responseModel
                                     .Resources
                                     ?.FirstOrDefault(r => r.Type.StartsWith("PackageBaseAddress"))
                                     ?.Id
                                 ?? throw new WebException(
                                     $"Couldn't find a valid PackageBaseAddress from nuget server with path: '{serverRoot}'!");
        var registrationsBaseUrl = responseModel
                                       .Resources
                                       .OrderByDescending(r => r.Type)
                                       .FirstOrDefault(r => r.Type.StartsWith("RegistrationsBaseUrl"))
                                       ?.Id
                                   ?? throw new WebException(
                                       $"Couldn't find a valid RegistrationsBaseUrl from nuget server with path: '{serverRoot}'!");
        
        // Rewrite URLs if using nuget.azure.cn
        if (serverRoot.Contains("nuget.azure.cn"))
        {
            packageBaseAddress = packageBaseAddress.Replace("api.nuget.org", "nuget.azure.cn");
            registrationsBaseUrl = registrationsBaseUrl.Replace("api.nuget.org", "nuget.azure.cn");
        }
        
        return new NugetServerEndPoints(packageBaseAddress, registrationsBaseUrl);
    }

    private async Task<IReadOnlyCollection<NugetVersion>> GetAllPublishedVersionsFromNuget(string packageName)
    {
        var apiEndpoint = await GetApiEndpoint() ?? throw new InvalidOperationException("Can NOT locate a valid nuget API endpoint!");
        var requestUrl = $"{apiEndpoint.PackageBaseAddress.TrimEnd('/')}/{packageName.ToLower()}/index.json";
        var responseModel = await HttpGetJson<GetAllPublishedVersionsResponseModel>(requestUrl, _patToken);
        return responseModel
                   .Versions
                   ?.Select(v => new NugetVersion(v))
                   .Where(v => _allowPreview || !v.IsPreviewVersion())
                   .ToList()
                   .AsReadOnly()
               ?? throw new WebException($"Couldn't find a valid version from Nuget with package: '{packageName}'!");
    }

    private async Task<CatalogInformation> GetPackageDeprecationInfoFromNuget(Package package)
    {
        try
        {
            var deprecation = await GetDeprecationFromSearch(package);
            var vulnerabilities = await GetVulnerabilitiesForPackage(package);

            return new CatalogInformation
            {
                Deprecation = deprecation,
                Vulnerabilities = vulnerabilities
            };
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to get package deprecation/vulnerability info for '{Package}' from nuget.org",
                package);
            return new CatalogInformation();
        }
    }

    private async Task<Deprecation?> GetDeprecationFromSearch(Package package)
    {
        var searchUrl =
            $"{NuGetSearchUrl}?q={Uri.EscapeDataString(package.Name)}&semVerLevel=2.0.0&prerelease=true&take=5";
        var response = await HttpGetJson<NuGetSearchResponse>(searchUrl, string.Empty);
        var match = response.Data.FirstOrDefault(d =>
            string.Equals(d.Id, package.Name, StringComparison.OrdinalIgnoreCase));
        return match?.Deprecation;
    }

    private async Task<IReadOnlyCollection<Vulnerability>?> GetVulnerabilitiesForPackage(Package package)
    {
        if (_vulnerabilityCache == null)
        {
            try
            {
                _vulnerabilityCache = await HttpGetJson<Dictionary<string, List<VulnerabilityBaseEntry>>>(
                    NuGetVulnerabilityBaseUrl, string.Empty);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to fetch vulnerability database from nuget.org");
                _vulnerabilityCache = new Dictionary<string, List<VulnerabilityBaseEntry>>();
            }
        }

        var key = package.Name.ToLower();
        if (_vulnerabilityCache.TryGetValue(key, out var entries))
        {
            var matching = entries
                .Where(e => IsVersionInRange(package.Version, e.Versions))
                .Select(e => new Vulnerability
                {
                    Id = e.Url,
                    Type = "PackageVulnerability",
                    AdvisoryUrl = e.Url,
                    Severity = e.Severity.ToString()
                })
                .ToList();

            return matching.Count > 0 ? matching.AsReadOnly() : null;
        }

        return null;
    }

    private static bool IsVersionInRange(NugetVersion version, string range)
    {
        if (string.IsNullOrWhiteSpace(range))
            return true;

        range = range.Trim();
        // Exact version: [1.0.0]
        if (!range.Contains(","))
        {
            var exact = range.Trim('[', ']', '(', ')').Trim();
            if (string.IsNullOrWhiteSpace(exact))
                return false;
            return version.Equals(new NugetVersion(exact));
        }

        var lowerInclusive = range.StartsWith('[');
        var upperInclusive = range.EndsWith(']');
        var parts = range.Trim('[', ']', '(', ')').Split(',');

        var lowerStr = parts[0].Trim();
        var upperStr = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        if (!string.IsNullOrWhiteSpace(lowerStr))
        {
            var lowerVersion = new NugetVersion(lowerStr);
            var cmp = version.CompareTo(lowerVersion);
            if (lowerInclusive ? cmp < 0 : cmp <= 0)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(upperStr))
        {
            var upperVersion = new NugetVersion(upperStr);
            var cmp = version.CompareTo(upperVersion);
            if (upperInclusive ? cmp > 0 : cmp >= 0)
                return false;
        }

        return true;
    }

    private async Task<Package[]> GetPackageDependenciesFromNuget(Package package)
    {
        var apiEndpoint = await GetApiEndpoint() ?? throw new InvalidOperationException("Can NOT locate a valid nuget API endpoint!");
        var requestUrl =
            $"{apiEndpoint.PackageBaseAddress.TrimEnd('/')}/{package.Name.ToLower()}/{package.Version}/{package.Name.ToLower()}.nuspec";
        var nuspec = await HttpGetString(requestUrl, _patToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(nuspec);
        var packageReferences = doc.DocumentNode
            .Descendants("dependency")
            .Select(p => new Package(
                p.Attributes["id"].Value,
                p.Attributes["version"].Value))
            .DistinctBy(p => p.Name)
            .ToArray();
        return packageReferences;
    }

    private async Task<T> HttpGetJson<T>(string url, string patToken)
    {
        var json = await HttpGetString(url, patToken);
        try
        {
            return JsonSerializer.Deserialize<T>(json) ??
                   throw new WebException($"The remote server returned null when deserializing: '{json}'");
        }
        catch (JsonException e)
        {
            throw new WebException($"The remote server returned non-json content: '{json}'", e);
        }
    }

    private async Task<string> HttpGetString(string url, string patToken)
    {
        try
        {
            return await HttpGetStringInternal(url, patToken);
        }
        catch (Exception e)
        {
            if (url.Contains("api.nuget.org"))
            {
                var fallbackUrl = url.Replace("api.nuget.org", "globalcdn.nuget.org");
                _logger.LogWarning(e, "Failed to fetch from {Url}. Retrying with fallback: {FallbackUrl}", url, fallbackUrl);
                return await HttpGetStringInternal(fallbackUrl, patToken);
            }

            throw;
        }
    }

    private async Task<string> HttpGetStringInternal(string url, string patToken)
    {
        _logger.LogTrace("Sending request to: {Url}...", url);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Aiursoft.NugetNinja");
        if (!string.IsNullOrWhiteSpace(patToken))
            request.Headers.Add("Authorization", StringExtensions.PatToHeader(patToken));
        using var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var isGZipEncoded = response.Content.Headers.ContentEncoding.Contains("gzip");
            if (isGZipEncoded)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var decompressionStream = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new StreamReader(decompressionStream);
                var text = await reader.ReadToEndAsync();
                return text;
            }

            return await response.Content.ReadAsStringAsync();
        }

        throw new WebException(
            $"The remote server returned unexpected status code: {response.StatusCode} - {response.ReasonPhrase}. Url: {url}.");
    }
}