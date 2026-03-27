using System.Collections.Concurrent;
using DevOpsCopilot.Models;

namespace DevOpsCopilot.Services.Memory;

/// <summary>
/// In-memory session store for local development and testing.
/// Data is lost when the process restarts.
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private readonly ConcurrentDictionary<string, List<ConversationMessage>> _messages = new();

    public Task<SessionInfo> CreateSessionAsync(string userId, string? projectName, string title)
    {
        var session = new SessionInfo
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            ProjectName = projectName,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
        };

        _sessions[session.SessionId] = session;
        _messages[session.SessionId] = new List<ConversationMessage>();
        return Task.FromResult(session);
    }

    public Task<SessionInfo?> GetSessionAsync(string sessionId, string userId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)
            || !string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<SessionInfo?>(null);
        }

        var result = CloneSession(session);
        result.Messages = _messages.TryGetValue(sessionId, out var msgs)
            ? new List<ConversationMessage>(msgs)
            : new List<ConversationMessage>();
        result.MessageCount = result.Messages.Count;
        return Task.FromResult<SessionInfo?>(result);
    }

    public Task<List<SessionInfo>> ListSessionsAsync(string userId, string? projectName = null, int skip = 0, int take = 50)
    {
        var query = _sessions.Values
            .Where(s => string.Equals(s.UserId, userId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(projectName))
            query = query.Where(s => string.IsNullOrEmpty(s.ProjectName)
                || string.Equals(s.ProjectName, projectName, StringComparison.OrdinalIgnoreCase));

        var result = query
            .OrderByDescending(s => s.LastActiveAt)
            .Skip(skip)
            .Take(take)
            .Select(s =>
            {
                var clone = CloneSession(s);
                clone.MessageCount = _messages.TryGetValue(s.SessionId, out var msgs) ? msgs.Count : 0;
                return clone;
            })
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddMessageAsync(string sessionId, string role, string content)
    {
        if (_messages.TryGetValue(sessionId, out var msgs))
        {
            msgs.Add(new ConversationMessage { Role = role, Content = content });
        }

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActiveAt = DateTime.UtcNow;
            session.MessageCount = msgs?.Count ?? 0;
        }

        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(string sessionId, string userId)
    {
        if (_sessions.TryGetValue(sessionId, out var session)
            && string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            _sessions.TryRemove(sessionId, out _);
            _messages.TryRemove(sessionId, out _);
        }
        return Task.CompletedTask;
    }

    public Task UpdateSessionTitleAsync(string sessionId, string userId, string title)
    {
        if (_sessions.TryGetValue(sessionId, out var session)
            && string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            session.Title = title;
        }
        return Task.CompletedTask;
    }

    private static SessionInfo CloneSession(SessionInfo s) => new()
    {
        SessionId = s.SessionId,
        UserId = s.UserId,
        ProjectName = s.ProjectName,
        Title = s.Title,
        CreatedAt = s.CreatedAt,
        LastActiveAt = s.LastActiveAt,
        MessageCount = s.MessageCount,
    };
}
