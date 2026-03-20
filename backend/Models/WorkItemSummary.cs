using System.Text.Json.Serialization;

namespace DevOpsCopilot.Models;

/// <summary>
/// Lightweight representation of an Azure DevOps work item for display in the UI.
/// </summary>
public sealed class WorkItemSummary
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("workItemType")]
    public string WorkItemType { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("assignedTo")]
    public string? AssignedTo { get; init; }

    [JsonPropertyName("areaPath")]
    public string? AreaPath { get; init; }

    [JsonPropertyName("iterationPath")]
    public string? IterationPath { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; init; }

    [JsonPropertyName("changedDate")]
    public DateTime? ChangedDate { get; init; }

    [JsonPropertyName("tags")]
    public string? Tags { get; init; }

    [JsonPropertyName("priority")]
    public int? Priority { get; init; }
}
