using System.Text.Json.Serialization;

namespace DevOpsCopilot.Models;

/// <summary>
/// A single SSE event sent during streaming chat.
/// Each event has a type so the frontend can display processing steps.
/// </summary>
public sealed class StreamEvent
{
    /// <summary>
    /// Event type: "step" (processing step), "content" (token chunk),
    /// "suggestedActions" (follow-up suggestions), "error", "done".
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Text content — token chunk for "content", description for "step", error message for "error".
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// Step identifier for "step" events: "routing", "agent", "tool", "thinking", "formatting".
    /// </summary>
    [JsonPropertyName("step")]
    public string? Step { get; init; }

    /// <summary>
    /// Agent name for "agent" step events.
    /// </summary>
    [JsonPropertyName("agent")]
    public string? Agent { get; init; }

    /// <summary>
    /// Tool name for "tool" step events.
    /// </summary>
    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    /// <summary>
    /// Suggested follow-up actions (for "suggestedActions" type).
    /// </summary>
    [JsonPropertyName("suggestedActions")]
    public List<string>? SuggestedActions { get; init; }
}

/// <summary>
/// Model info returned by the /api/models endpoint.
/// </summary>
public sealed class ModelInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; init; }
}
