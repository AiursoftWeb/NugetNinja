using System.Text.Json.Serialization;

namespace Aiursoft.NugetNinja.GeminiBot.Models;

public class PullRequest
{
    [JsonPropertyName("user")] public User? User { get; set; }

    [JsonPropertyName("state")] public string? State { get; set; }
}

public class User
{
    /// <summary>
    /// </summary>
    [JsonPropertyName("login")]
    public string? Login { get; set; }
}


public class Repository
{
    /// <summary>
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// </summary>
    [JsonPropertyName("full_name")]
    public string? FullName { get; set; }

    /// <summary>
    /// </summary>
    [JsonPropertyName("owner")]
    public User? Owner { get; set; }

    /// <summary>
    /// </summary>
    [JsonPropertyName("default_branch")]
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// </summary>
    [JsonPropertyName("clone_url")]
    public string? CloneUrl { get; set; }

    /// <summary>
    /// </summary>
    [JsonPropertyName("archived")]
    public bool? Archived { get; set; }

    public override string ToString()
    {
        return FullName ?? throw new NullReferenceException($"The {nameof(FullName)} of this repo is null!");
    }
}