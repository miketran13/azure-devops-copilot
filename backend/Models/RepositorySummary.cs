using System.Text.Json.Serialization;

namespace DevOpsCopilot.Models;

/// <summary>
/// Lightweight representation of an Azure DevOps Git repository.
/// </summary>
public sealed class RepositorySummary
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("defaultBranch")]
    public string? DefaultBranch { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }
}
