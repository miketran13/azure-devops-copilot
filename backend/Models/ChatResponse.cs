using System.Text.Json.Serialization;

namespace DevOpsCopilot.Models;

/// <summary>
/// Response from the chat endpoint back to the extension frontend.
/// </summary>
public sealed class ChatResponse
{
    /// <summary>
    /// The AI agent's reply text.
    /// </summary>
    [JsonPropertyName("reply")]
    public required string Reply { get; init; }

    /// <summary>
    /// Work items referenced or created during the conversation turn.
    /// </summary>
    [JsonPropertyName("workItems")]
    public List<WorkItemSummary>? WorkItems { get; init; }

    /// <summary>
    /// Suggested follow-up actions for the user.
    /// </summary>
    [JsonPropertyName("suggestedActions")]
    public List<string>? SuggestedActions { get; init; }
}
