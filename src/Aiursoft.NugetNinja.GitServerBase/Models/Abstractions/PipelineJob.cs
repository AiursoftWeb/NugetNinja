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

    // For filtering jobs when getting all project jobs
    [JsonProperty("pipeline")]
    public PipelineReference? Pipeline { get; set; }

    // Helper property to get pipeline ID
    public int PipelineId => Pipeline?.Id ?? 0;
}

public class PipelineReference
{
    [JsonProperty("id")]
    public int Id { get; set; }
}
