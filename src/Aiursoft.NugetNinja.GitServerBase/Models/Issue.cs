using System.Text.Json.Serialization;

namespace Aiursoft.NugetNinja.GitServerBase.Models;

public class Issue
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("iid")]
    public int Iid { get; set; }

    [JsonPropertyName("project_id")]
    public int ProjectId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("web_url")]
    public string? WebUrl { get; set; }

    [JsonPropertyName("author")]
    public User? Author { get; set; }

    public override string ToString()
    {
        return $"Issue #{Iid}: {Title}";
    }
}
