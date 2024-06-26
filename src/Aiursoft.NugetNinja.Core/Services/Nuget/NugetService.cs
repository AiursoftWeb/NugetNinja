﻿using System.IO.Compression;
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
    private readonly string _customNugetServer = DefaultNugetServer;
    private readonly string _patToken;
    private readonly bool _allowPreview;
    private readonly bool _allowPackageVersionCrossMicrosoftRuntime;
    private readonly CacheService _cacheService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NugetService> _logger;
    private readonly VersionCrossChecker _versionCrossChecker;

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
                var versionString = $"{v.PrimaryVersion.Major}.{v.PrimaryVersion.Minor}";
                return runtimes.Any(r => r.Contains(versionString));
            });
            return latest != null ? latest : allVersions.First();
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
        if (!serverRoot.EndsWith("index.json")) serverRoot += "/index.json";
        if (!serverRoot.EndsWith("v3/index.json")) serverRoot += "/v3/index.json";

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

    private async Task<CatalogInformation> GetPackageDeprecationInfoFromNuget(Package package,
        string? overrideServer = null, string? overridePat = null)
    {
        var server = overrideServer ?? _customNugetServer;
        var pat = overridePat ?? _patToken;
        try
        {
            var apiEndpoint = await GetApiEndpoint(server) ?? throw new InvalidOperationException("Can NOT locate a valid nuget API endpoint!");
            var requestUrl =
                $"{apiEndpoint.RegistrationsBaseUrl.TrimEnd('/')}/{package.Name.ToLower()}/{package.Version.ToString().ToLower()}.json";
            var packageContext = await HttpGetJson<RegistrationIndex>(requestUrl, pat);
            var packageCatalogUrl = packageContext.CatalogEntry ??
                                    throw new WebException(
                                        $"Couldn't find a valid catalog entry for package: '{package}'!");
            return await HttpGetJson<CatalogInformation>(packageCatalogUrl, pat);
        }
        catch
        {
            if (server != DefaultNugetServer)
                // fall back to default server.
                return await GetPackageDeprecationInfoFromNuget(package, DefaultNugetServer, string.Empty);
            throw;
        }
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
        return JsonSerializer.Deserialize<T>(json) ??
               throw new WebException($"The remote server returned non-json content: '{json}'");
    }

    private async Task<string> HttpGetString(string url, string patToken)
    {
        _logger.LogTrace("Sending request to: {Url}...", url);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
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
            else
            {
                var text = await response.Content.ReadAsStringAsync();
                return text;
            }
        }

        throw new WebException(
            $"The remote server returned unexpected status code: {response.StatusCode} - {response.ReasonPhrase}. Url: {url}.");
    }
}