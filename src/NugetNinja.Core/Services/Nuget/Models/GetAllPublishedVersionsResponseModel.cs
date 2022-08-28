

using System.Text.Json.Serialization;

namespace Aiursoft.NugetNinja.Core;

public class GetAllPublishedVersionsResponseModel
{
    [JsonPropertyName("versions")]
    public List<string>? Versions { get; set; }
}
