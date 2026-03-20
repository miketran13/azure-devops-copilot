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
