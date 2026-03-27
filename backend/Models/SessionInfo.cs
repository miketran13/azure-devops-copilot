using System.Text.Json.Serialization;

namespace DevOpsCopilot.Models;

/// <summary>
/// Represents a persistent chat session with its metadata and messages.
/// </summary>
public sealed class SessionInfo
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "New Chat";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastActiveAt")]
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("messageCount")]
    public int MessageCount { get; set; }

    [JsonPropertyName("messages")]
    public List<ConversationMessage>? Messages { get; set; }
}
