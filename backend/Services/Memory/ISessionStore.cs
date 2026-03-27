using DevOpsCopilot.Models;

namespace DevOpsCopilot.Services.Memory;

/// <summary>
/// Abstraction for session/conversation persistence.
/// Implement this interface to add a new storage backend.
///
/// Built-in implementations:
/// - InMemorySessionStore (dev/testing)
/// - AzureTableSessionStore (production default)
/// - NullSessionStore (when memory is disabled)
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Create a new session for a user.
    /// </summary>
    Task<SessionInfo> CreateSessionAsync(string userId, string? projectName, string title);

    /// <summary>
    /// Get a session with its messages. Returns null if not found or not owned by user.
    /// </summary>
    Task<SessionInfo?> GetSessionAsync(string sessionId, string userId);

    /// <summary>
    /// List sessions for a user, optionally filtered by project. Ordered by LastActiveAt descending.
    /// </summary>
    Task<List<SessionInfo>> ListSessionsAsync(string userId, string? projectName = null, int skip = 0, int take = 50);

    /// <summary>
    /// Add a message to an existing session.
    /// </summary>
    Task AddMessageAsync(string sessionId, string role, string content);

    /// <summary>
    /// Delete a session and all its messages. Only succeeds if owned by user.
    /// </summary>
    Task DeleteSessionAsync(string sessionId, string userId);

    /// <summary>
    /// Update the title of a session.
    /// </summary>
    Task UpdateSessionTitleAsync(string sessionId, string userId, string title);
}
