using System.Text.Json.Serialization;

namespace Aiursoft.NugetNinja.Core.Services.Nuget.Models;

public class GetAllPublishedVersionsResponseModel
{
    [JsonPropertyName("versions")] public IReadOnlyCollection<string>? Versions { get; init; }
}