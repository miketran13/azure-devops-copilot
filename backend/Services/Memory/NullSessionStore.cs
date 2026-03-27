using DevOpsCopilot.Models;

namespace DevOpsCopilot.Services.Memory;

/// <summary>
/// No-op session store used when persistent memory is disabled.
/// All operations return empty results or succeed silently.
/// </summary>
public sealed class NullSessionStore : ISessionStore
{
    public Task<SessionInfo> CreateSessionAsync(string userId, string? projectName, string title)
        => Task.FromResult(new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            ProjectName = projectName,
            Title = title,
        });

    public Task<SessionInfo?> GetSessionAsync(string sessionId, string userId)
        => Task.FromResult<SessionInfo?>(null);

    public Task<List<SessionInfo>> ListSessionsAsync(string userId, string? projectName = null, int skip = 0, int take = 50)
        => Task.FromResult(new List<SessionInfo>());

    public Task AddMessageAsync(string sessionId, string role, string content)
        => Task.CompletedTask;

    public Task DeleteSessionAsync(string sessionId, string userId)
        => Task.CompletedTask;

    public Task UpdateSessionTitleAsync(string sessionId, string userId, string title)
        => Task.CompletedTask;
}
