using Newtonsoft.Json;

namespace Aiursoft.NugetNinja.MergeBot.Models.Abstractions;

public class MergeRequestSearchResult
{
    [JsonProperty("id")]
    public int Id { get; set; }
    
    // ReSharper disable once InconsistentNaming
    [JsonProperty("iid")]
    public int IID { get; set; }
    
    [JsonProperty("project_id")]
    public int ProjectId { get; set; }
    
    [JsonProperty("title")]
    public string? Title { get; set; }
    
    [JsonProperty("has_conflicts")]
    public bool HasConflicts { get; set; }
    
    [JsonProperty("draft")]
    public bool Draft { get; set; }
    
    [JsonProperty("work_in_progress")]
    public bool WorkInProgress { get; set; }
}