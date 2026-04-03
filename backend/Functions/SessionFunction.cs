using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DevOpsCopilot.Services;
using DevOpsCopilot.Services.Memory;

namespace DevOpsCopilot.Functions;

/// <summary>
/// API endpoints for session/conversation history management.
/// Only active when memory is enabled in Config/memory.json.
/// </summary>
public sealed class SessionFunction
{
    private readonly ISessionStore _sessionStore;
    private readonly TokenValidationService _tokenService;
    private readonly ILogger<SessionFunction> _logger;
    private readonly string _appMode;

    public SessionFunction(
        ISessionStore sessionStore,
        TokenValidationService tokenService,
        IConfiguration configuration,
        ILogger<SessionFunction> logger)
    {
        _sessionStore = sessionStore;
        _tokenService = tokenService;
        _appMode = configuration.GetValue<string>("AppMode") ?? "AzureDevOps";
        _logger = logger;
    }

    /// <summary>
    /// GET /api/sessions — list the current user's sessions.
    /// Optional query params: projectName, skip, take.
    /// </summary>
    [Function("ListSessions")]
    public async Task<IActionResult> ListSessions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions")] HttpRequest req)
    {
        var (userId, error) = ValidateAndGetUserId(req);
        if (error is not null) return error;

        var projectName = req.Query["projectName"].FirstOrDefault();
        _ = int.TryParse(req.Query["skip"].FirstOrDefault(), out var skip);
        var take = int.TryParse(req.Query["take"].FirstOrDefault(), out var t) ? t : 50;

        var sessions = await _sessionStore.ListSessionsAsync(userId!, projectName, skip, take);
        return new OkObjectResult(sessions);
    }

    /// <summary>
    /// GET /api/sessions/{id} — get a session with its messages.
    /// </summary>
    [Function("GetSession")]
    public async Task<IActionResult> GetSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions/{id}")] HttpRequest req,
        string id)
    {
        var (userId, error) = ValidateAndGetUserId(req);
        if (error is not null) return error;

        var session = await _sessionStore.GetSessionAsync(id, userId!);
        if (session is null)
            return new NotFoundObjectResult(new { error = "Session not found." });

        return new OkObjectResult(session);
    }

    /// <summary>
    /// POST /api/sessions — create a new session.
    /// Body: { "projectName": "...", "title": "..." }
    /// </summary>
    [Function("CreateSession")]
    public async Task<IActionResult> CreateSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions")] HttpRequest req)
    {
        var (userId, error) = ValidateAndGetUserId(req);
        if (error is not null) return error;

        var body = await JsonSerializer.DeserializeAsync<CreateSessionRequest>(req.Body, _jsonOptions);
        var title = body?.Title ?? "New Chat";
        var projectName = body?.ProjectName;

        var session = await _sessionStore.CreateSessionAsync(userId!, projectName, title);
        return new OkObjectResult(session);
    }

    /// <summary>
    /// DELETE /api/sessions/{id} — delete a session.
    /// </summary>
    [Function("DeleteSession")]
    public async Task<IActionResult> DeleteSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sessions/{id}")] HttpRequest req,
        string id)
    {
        var (userId, error) = ValidateAndGetUserId(req);
        if (error is not null) return error;

        await _sessionStore.DeleteSessionAsync(id, userId!);
        return new OkResult();
    }

    /// <summary>
    /// PATCH /api/sessions/{id} — rename a session.
    /// Body: { "title": "..." }
    /// </summary>
    [Function("UpdateSession")]
    public async Task<IActionResult> UpdateSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "sessions/{id}")] HttpRequest req,
        string id)
    {
        var (userId, error) = ValidateAndGetUserId(req);
        if (error is not null) return error;

        var body = await JsonSerializer.DeserializeAsync<UpdateSessionRequest>(req.Body, _jsonOptions);
        if (string.IsNullOrWhiteSpace(body?.Title))
            return new BadRequestObjectResult(new { error = "Title is required." });

        await _sessionStore.UpdateSessionTitleAsync(id, userId!, body.Title);
        return new OkResult();
    }

    /// <summary>
    /// POST /api/sessions/{id}/messages — add a message to an existing session.
    /// Body: { "role": "user"|"assistant", "content": "..." }
    /// </summary>
    [Function("AddMessage")]
    public async Task<IActionResult> AddMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sessions/{id}/messages")] HttpRequest req,
        string id)
    {
        var (userId, error) = ValidateAndGetUserId(req);
        if (error is not null) return error;

        var body = await JsonSerializer.DeserializeAsync<AddMessageRequest>(req.Body, _jsonOptions);
        if (string.IsNullOrWhiteSpace(body?.Role) || string.IsNullOrWhiteSpace(body?.Content))
            return new BadRequestObjectResult(new { error = "Role and content are required." });

        // Verify session exists and belongs to user
        var session = await _sessionStore.GetSessionAsync(id, userId!);
        if (session is null)
            return new NotFoundObjectResult(new { error = "Session not found." });

        await _sessionStore.AddMessageAsync(id, body.Role, body.Content);
        return new OkResult();
    }

    private (string? UserId, IActionResult? Error) ValidateAndGetUserId(HttpRequest req)
    {
        var isStandaloneMode = string.Equals(_appMode, "Standalone", StringComparison.OrdinalIgnoreCase);
        var isBothMode = string.Equals(_appMode, "Both", StringComparison.OrdinalIgnoreCase);

        var githubToken = req.Headers["X-GitHub-Token"].FirstOrDefault();
        var hasGithubToken = !string.IsNullOrEmpty(githubToken);

        if (isStandaloneMode || (isBothMode && hasGithubToken))
        {
            if (!hasGithubToken)
                return (null, new UnauthorizedObjectResult(new { error = "Missing X-GitHub-Token header." }));

            // Derive a stable user ID from the hashed GitHub token
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(githubToken!));
            var userId = Convert.ToHexStringLower(hash[..8]);
            return (userId, null);
        }

        // Standard Azure DevOps mode
        var appToken = req.Headers["X-Extension-Token"].FirstOrDefault();
        if (!_tokenService.ValidateAppToken(appToken))
            return (null, new UnauthorizedObjectResult(new { error = "Invalid extension token." }));

        var userToken = TokenValidationService.ExtractBearerToken(
            req.Headers.Authorization.FirstOrDefault());

        if (string.IsNullOrEmpty(userToken))
            return (null, new UnauthorizedObjectResult(new { error = "Missing Authorization bearer token." }));

        // Prefer the stable Azure DevOps user ID sent by the extension (SDK.getUser().id).
        // Falls back to hashing the bearer token, but bearer tokens rotate hourly
        // which breaks session ownership — so X-User-Id should always be sent.
        var stableUserId = req.Headers["X-User-Id"].FirstOrDefault();
        var userId2 = !string.IsNullOrEmpty(stableUserId)
            ? stableUserId
            : Convert.ToBase64String(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(userToken)))[..16];

        return (userId2, null);
    }

    // Case-insensitive options so camelCase JSON from the frontend maps correctly
    // to PascalCase C# record properties (System.Text.Json is case-sensitive by default).
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record CreateSessionRequest(string? ProjectName, string? Title);
    private sealed record UpdateSessionRequest(string? Title);
    private sealed record AddMessageRequest(string? Role, string? Content);
}
