using Newtonsoft.Json;

namespace Aiursoft.NugetNinja.MergeBot.Models.Abstractions;

public class PipelineResult
{
    [JsonProperty("id")]
    public int Id { get; set; }
    
    [JsonProperty("status")]
    public string? Status { get; set; }
    
    [JsonProperty("web_url")]
    public string? WebUrl { get; set; }
}