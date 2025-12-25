using Newtonsoft.Json;

namespace Aiursoft.NugetNinja.GitServerBase.Models.Abstractions;

public class DetailedMergeRequest : MergeRequestSearchResult
{
    /// <summary>
    /// The pipeline associated with the Merge Request itself (Detached MR Pipeline or Merge Train).
    /// This is null for most projects unless they have configured special MR pipelines.
    /// </summary>
    [JsonProperty("pipeline")]
    public PipelineResult? MrPipeline { get; set; }

    /// <summary>
    /// The pipeline from the latest commit on the source branch (Head Commit Pipeline).
    /// This is what most users see in the GitLab UI as "Pipeline #XXXX failed/passed".
    /// This field takes precedence over 'pipeline' when both exist.
    /// </summary>
    [JsonProperty("head_pipeline")]
    public PipelineResult? HeadPipeline { get; set; }

    /// <summary>
    /// Gets the actual pipeline to check. Prefers HeadPipeline (source branch) over MrPipeline (detached).
    /// Returns the pipeline that represents what's shown in the GitLab UI.
    /// </summary>
    [JsonIgnore]
    public PipelineResult? Pipeline => HeadPipeline ?? MrPipeline;
}