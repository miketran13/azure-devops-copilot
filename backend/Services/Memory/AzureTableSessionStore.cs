using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using DevOpsCopilot.Models;

namespace DevOpsCopilot.Services.Memory;

/// <summary>
/// Azure Table Storage session store for production scalability.
/// Schema: PartitionKey = userId, RowKey = sessionId, Messages as JSON blob.
/// Suitable for multi-instance Azure Functions deployments.
/// </summary>
public sealed class AzureTableSessionStore : ISessionStore
{
    private const string TableName = "CopilotSessions";
    private readonly TableClient _tableClient;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public AzureTableSessionStore(string connectionString)
    {
        var serviceClient = new TableServiceClient(connectionString);
        _tableClient = serviceClient.GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<SessionInfo> CreateSessionAsync(string userId, string? projectName, string title)
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

        var entity = ToEntity(session);
        await _tableClient.AddEntityAsync(entity);
        return StripMessages(session);
    }

    public async Task<SessionInfo?> GetSessionAsync(string sessionId, string userId)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(userId, sessionId);
            return FromEntity(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<List<SessionInfo>> ListSessionsAsync(
        string userId, string? projectName = null, int skip = 0, int take = 50)
    {
        var filter = $"PartitionKey eq '{EscapeFilter(userId)}'";
        if (!string.IsNullOrEmpty(projectName))
        {
            filter += $" and (ProjectName eq '{EscapeFilter(projectName)}' or ProjectName eq '')";
        }

        var sessions = new List<SessionInfo>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter))
        {
            var session = FromEntity(entity);
            if (session is not null)
                sessions.Add(StripMessages(session));
        }

        return sessions
            .OrderByDescending(s => s.LastActiveAt)
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    public async Task AddMessageAsync(string sessionId, string role, string content)
    {
        try
        {
            // We need the userId (PartitionKey) to look up the entity.
            // Query by RowKey across all partitions — sessions are unique by ID.
            var filter = $"RowKey eq '{EscapeFilter(sessionId)}'";
            TableEntity? entity = null;
            await foreach (var e in _tableClient.QueryAsync<TableEntity>(filter, maxPerPage: 1))
            {
                entity = e;
                break;
            }
            if (entity is null) return;

            var session = FromEntity(entity);
            if (session is null) return;

            session.Messages ??= [];
            session.Messages.Add(new ConversationMessage { Role = role, Content = content });
            session.LastActiveAt = DateTime.UtcNow;
            session.MessageCount = session.Messages.Count;

            var updated = ToEntity(session);
            await _tableClient.UpsertEntityAsync(updated, TableUpdateMode.Replace);
        }
        catch (RequestFailedException)
        {
            // Silently fail on message add — non-critical
        }
    }

    public async Task DeleteSessionAsync(string sessionId, string userId)
    {
        try
        {
            await _tableClient.DeleteEntityAsync(userId, sessionId);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already deleted
        }
    }

    public async Task UpdateSessionTitleAsync(string sessionId, string userId, string title)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(userId, sessionId);
            var entity = response.Value;
            entity["Title"] = title;
            await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Session not found — no-op
        }
    }

    // ─── Entity mapping ────────────────────────────────────────────────

    private static TableEntity ToEntity(SessionInfo session)
    {
        var entity = new TableEntity(session.UserId, session.SessionId)
        {
            ["ProjectName"] = session.ProjectName ?? string.Empty,
            ["Title"] = session.Title,
            ["CreatedAt"] = session.CreatedAt,
            ["LastActiveAt"] = session.LastActiveAt,
            ["MessageCount"] = session.MessageCount,
            ["MessagesJson"] = JsonSerializer.Serialize(session.Messages ?? [], _jsonOpts),
        };
        return entity;
    }

    private static SessionInfo? FromEntity(TableEntity entity)
    {
        try
        {
            var messagesJson = entity.GetString("MessagesJson") ?? "[]";
            var messages = JsonSerializer.Deserialize<List<ConversationMessage>>(messagesJson, _jsonOpts) ?? [];

            return new SessionInfo
            {
                SessionId = entity.RowKey,
                UserId = entity.PartitionKey,
                ProjectName = entity.GetString("ProjectName"),
                Title = entity.GetString("Title") ?? "New Chat",
                CreatedAt = entity.GetDateTimeOffset("CreatedAt")?.UtcDateTime ?? DateTime.UtcNow,
                LastActiveAt = entity.GetDateTimeOffset("LastActiveAt")?.UtcDateTime ?? DateTime.UtcNow,
                MessageCount = entity.GetInt32("MessageCount") ?? 0,
                Messages = messages,
            };
        }
        catch
        {
            return null;
        }
    }

    private static SessionInfo StripMessages(SessionInfo session) => new()
    {
        SessionId = session.SessionId,
        UserId = session.UserId,
        ProjectName = session.ProjectName,
        Title = session.Title,
        CreatedAt = session.CreatedAt,
        LastActiveAt = session.LastActiveAt,
        MessageCount = session.MessageCount,
        Messages = null,
    };

    private static string EscapeFilter(string value) =>
        value.Replace("'", "''");
}
