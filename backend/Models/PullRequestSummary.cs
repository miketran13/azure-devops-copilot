using System.Text.Json.Serialization;

namespace DevOpsCopilot.Models;

/// <summary>
/// Lightweight representation of an Azure DevOps pull request.
/// </summary>
public sealed class PullRequestSummary
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("sourceBranch")]
    public string SourceBranch { get; init; } = string.Empty;

    [JsonPropertyName("targetBranch")]
    public string TargetBranch { get; init; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; init; }

    [JsonPropertyName("reviewers")]
    public List<string>? Reviewers { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("labels")]
    public List<string>? Labels { get; init; }

    [JsonPropertyName("mergeStatus")]
    public string? MergeStatus { get; init; }

    [JsonPropertyName("creationDate")]
    public DateTime? CreationDate { get; init; }

    [JsonPropertyName("closedDate")]
    public DateTime? ClosedDate { get; init; }

    [JsonPropertyName("repository")]
    public string? Repository { get; init; }
}
