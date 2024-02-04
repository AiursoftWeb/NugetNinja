using Newtonsoft.Json;

namespace Aiursoft.NugetNinja.MergeBot.Models.Abstractions;

public class DetailedMergeRequest : MergeRequestSearchResult
{
    [JsonProperty("pipeline")]
    public PipelineResult? Pipeline { get; set; }
}