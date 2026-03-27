using System.Text.Json.Serialization;

namespace DevOpsCopilot.Models;

/// <summary>
/// Incoming chat request from the extension frontend.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>
    /// The user's message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Previous conversation messages for multi-turn context.
    /// </summary>
    [JsonPropertyName("conversationHistory")]
    public List<ConversationMessage>? ConversationHistory { get; init; }

    /// <summary>
    /// Azure DevOps project name (from extension context).
    /// </summary>
    [JsonPropertyName("projectName")]
    public string? ProjectName { get; init; }

    /// <summary>
    /// Azure DevOps organization URL (from extension context).
    /// </summary>
    [JsonPropertyName("organizationUrl")]
    public string? OrganizationUrl { get; init; }

    /// <summary>
    /// Optional session ID for persistent memory. When provided, the backend
    /// loads/saves conversation history from the configured session store
    /// instead of relying on the client-sent conversationHistory.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>
    /// Optional model ID to use for this request. Must match one of the
    /// configured model IDs in AzureOpenAI:Models. Falls back to the default model.
    /// </summary>
    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; }

    /// <summary>
    /// Optional work item context when the chat is opened from a work item form.
    /// When present, the orchestrator injects this into the AI conversation so it
    /// knows which work item the user is referring to.
    /// </summary>
    [JsonPropertyName("workItemContext")]
    public WorkItemContextInfo? WorkItemContext { get; init; }
}

/// <summary>
/// Work item context passed from the extension frontend.
/// </summary>
public sealed class WorkItemContextInfo
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";
}

/// <summary>
/// A single message in conversation history.
/// </summary>
public sealed class ConversationMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}
