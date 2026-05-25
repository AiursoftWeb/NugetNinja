using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aiursoft.NugetNinja.Core.Services.Nuget.Models;

public class ResourcesItem
{
    [JsonPropertyName("@id")] public string Id { get; set; } = string.Empty;

    [JsonPropertyName("@type")] public string Type { get; set; } = string.Empty;
}

public class NugetServerIndex
{
    [JsonPropertyName("resources")] public IReadOnlyCollection<ResourcesItem>? Resources { get; init; }

    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
}

public class NugetServerEndPoints(string packageBaseAddress, string registrationsBaseUrl)
{
    /// <summary>
    ///     Base URL of Azure storage where NuGet package registration info is stored
    ///     Sample: https://api.nuget.org/v3/registration5-semver1/
    /// </summary>
    public string RegistrationsBaseUrl { get; set; } = registrationsBaseUrl;

    /// <summary>
    ///     Base URL of where NuGet packages are stored, in the format
    ///     https://api.nuget.org/v3-flatcontainer/{id-lower}/{version-lower}/{id-lower}.{version-lower}.nupkg
    ///     Sample: https://api.nuget.org/v3-flatcontainer/
    /// </summary>
    public string PackageBaseAddress { get; set; } = packageBaseAddress;
}

public class RegistrationIndex
{
    [JsonPropertyName("catalogEntry")]
    public JsonElement CatalogEntryElement { get; set; }

    public string? GetCatalogEntryUrl()
    {
        if (CatalogEntryElement.ValueKind == JsonValueKind.String)
            return CatalogEntryElement.GetString();
        if (CatalogEntryElement.ValueKind == JsonValueKind.Object &&
            CatalogEntryElement.TryGetProperty("@id", out var idElement))
            return idElement.GetString();
        return null;
    }

    public CatalogInformation? GetInlineCatalogInfo()
    {
        if (CatalogEntryElement.ValueKind != JsonValueKind.Object)
            return null;
        return CatalogEntryElement.Deserialize<CatalogInformation>();
    }
}

public class AlternatePackage
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("range")]
    public string? Range { get; set; }
}

public class Deprecation
{
    [JsonPropertyName("alternatePackage")]
    public AlternatePackage? AlternatePackage { get; set; }

    [JsonPropertyName("reasons")]
    public IReadOnlyCollection<string>? Reasons { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class CatalogInformation
{
    /// <summary>
    /// </summary>
    [JsonPropertyName("deprecation")]
    public Deprecation? Deprecation { get; set; }

    [JsonPropertyName("vulnerabilities")] public IReadOnlyCollection<Vulnerability>? Vulnerabilities { get; init; }
}

public class Vulnerability
{
    /// <summary>
    /// </summary>
    [JsonPropertyName("@id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// </summary>
    [JsonPropertyName("@type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// </summary>
    [JsonPropertyName("advisoryUrl")]
    public string AdvisoryUrl { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;
}

public class NuGetSearchResponse
{
    [JsonPropertyName("data")]
    public IReadOnlyCollection<NuGetSearchResult> Data { get; init; } = Array.Empty<NuGetSearchResult>();
}

public class NuGetSearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("deprecation")]
    public Deprecation? Deprecation { get; init; }

    [JsonPropertyName("vulnerabilities")]
    public IReadOnlyCollection<Vulnerability>? Vulnerabilities { get; init; }
}

public class VulnerabilityBaseEntry
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public int Severity { get; set; }

    [JsonPropertyName("versions")]
    public string Versions { get; set; } = string.Empty;
}