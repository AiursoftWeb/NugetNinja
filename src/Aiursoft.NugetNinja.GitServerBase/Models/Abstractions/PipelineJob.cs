using Newtonsoft.Json;

namespace Aiursoft.NugetNinja.GitServerBase.Models.Abstractions;

public class PipelineJob
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("stage")]
    public string? Stage { get; set; }

    [JsonProperty("web_url")]
    public string? WebUrl { get; set; }
}
