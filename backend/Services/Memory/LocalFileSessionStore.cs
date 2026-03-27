using System.Text.Json;
using DevOpsCopilot.Models;

namespace DevOpsCopilot.Services.Memory;

/// <summary>
/// File-based session store that persists sessions as JSON files in a local directory.
/// Each session is stored as a separate JSON file: {sessionId}.json
/// Suitable for local development and single-instance deployments.
/// </summary>
public sealed class LocalFileSessionStore : ISessionStore
{
    private readonly string _baseDir;
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public LocalFileSessionStore(string baseDirectory)
    {
        _baseDir = baseDirectory;
        Directory.CreateDirectory(_baseDir);
    }

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
            Messages = [],
        };

        WriteSession(session);
        return Task.FromResult(StripMessages(session));
    }

    public Task<SessionInfo?> GetSessionAsync(string sessionId, string userId)
    {
        var session = ReadSession(sessionId);
        if (session is null || !string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<SessionInfo?>(null);

        return Task.FromResult<SessionInfo?>(session);
    }

    public Task<List<SessionInfo>> ListSessionsAsync(string userId, string? projectName = null, int skip = 0, int take = 50)
    {
        var sessions = LoadAllSessions()
            .Where(s => string.Equals(s.UserId, userId, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(projectName))
            sessions = sessions.Where(s => string.IsNullOrEmpty(s.ProjectName)
                || string.Equals(s.ProjectName, projectName, StringComparison.OrdinalIgnoreCase));

        var result = sessions
            .OrderByDescending(s => s.LastActiveAt)
            .Skip(skip)
            .Take(take)
            .Select(StripMessages)
            .ToList();

        return Task.FromResult(result);
    }

    public Task AddMessageAsync(string sessionId, string role, string content)
    {
        lock (_lock)
        {
            var session = ReadSession(sessionId);
            if (session is null) return Task.CompletedTask;

            session.Messages ??= [];
            session.Messages.Add(new ConversationMessage { Role = role, Content = content });
            session.LastActiveAt = DateTime.UtcNow;
            session.MessageCount = session.Messages.Count;
            WriteSession(session);
        }

        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(string sessionId, string userId)
    {
        var session = ReadSession(sessionId);
        if (session is not null && string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            var path = GetSessionPath(sessionId);
            if (File.Exists(path)) File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public Task UpdateSessionTitleAsync(string sessionId, string userId, string title)
    {
        lock (_lock)
        {
            var session = ReadSession(sessionId);
            if (session is not null && string.Equals(session.UserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                session.Title = title;
                WriteSession(session);
            }
        }
        return Task.CompletedTask;
    }

    // ─── File I/O helpers ──────────────────────────────────────────────

    private string GetSessionPath(string sessionId) =>
        Path.Combine(_baseDir, $"{sessionId}.json");

    private SessionInfo? ReadSession(string sessionId)
    {
        var path = GetSessionPath(sessionId);
        if (!File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SessionInfo>(json, _jsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private void WriteSession(SessionInfo session)
    {
        var path = GetSessionPath(session.SessionId);
        var json = JsonSerializer.Serialize(session, _jsonOpts);
        File.WriteAllText(path, json);
    }

    private IEnumerable<SessionInfo> LoadAllSessions()
    {
        if (!Directory.Exists(_baseDir)) yield break;

        foreach (var file in Directory.EnumerateFiles(_baseDir, "*.json"))
        {
            SessionInfo? session = null;
            try
            {
                var json = File.ReadAllText(file);
                session = JsonSerializer.Deserialize<SessionInfo>(json, _jsonOpts);
            }
            catch
            {
                // Skip corrupt files
            }

            if (session is not null) yield return session;
        }
    }

    private static SessionInfo StripMessages(SessionInfo s) => new()
    {
        SessionId = s.SessionId,
        UserId = s.UserId,
        ProjectName = s.ProjectName,
        Title = s.Title,
        CreatedAt = s.CreatedAt,
        LastActiveAt = s.LastActiveAt,
        MessageCount = s.Messages?.Count ?? s.MessageCount,
    };
}
