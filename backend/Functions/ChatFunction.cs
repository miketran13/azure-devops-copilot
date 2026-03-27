using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DevOpsCopilot.Agents;
using DevOpsCopilot.Models;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Functions;

/// <summary>
/// Main chat endpoint — receives user messages from the Azure DevOps extension
/// and processes them through the multi-agent system.
/// </summary>
public sealed class ChatFunction : IDisposable
{
    private readonly AgentOrchestrator _orchestrator;
    private readonly TokenValidationService _tokenService;
    private readonly ILogger<ChatFunction> _logger;

    // Per-user rate limiting: 20 requests per minute with token bucket
    private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _rateLimiters = new();
    private static readonly TokenBucketRateLimiterOptions _rateLimitOptions = new()
    {
        TokenLimit = 20,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokensPerPeriod = 20,
        QueueLimit = 0,
    };

    public ChatFunction(
        AgentOrchestrator orchestrator,
        TokenValidationService tokenService,
        ILogger<ChatFunction> logger)
    {
        _orchestrator = orchestrator;
        _tokenService = tokenService;
        _logger = logger;
    }

    private static IActionResult? CheckRateLimit(string userToken)
    {
        // Use a hash of the token as the rate-limit key to avoid storing tokens
        var key = userToken.GetHashCode().ToString();
        var limiter = _rateLimiters.GetOrAdd(key, _ => new TokenBucketRateLimiter(_rateLimitOptions));

        using var lease = limiter.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            return new ObjectResult(new { error = "Too many requests. Please wait a moment and try again." })
            {
                StatusCode = 429,
            };
        }
        return null;
    }

    /// <summary>
    /// POST /api/chat — synchronous chat endpoint returning full JSON response.
    /// </summary>
    [Function("Chat")]
    public async Task<IActionResult> Chat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat")] HttpRequest req)
    {
        _logger.LogInformation("Chat request received");

        // Validate extension app token
        var appToken = req.Headers["X-Extension-Token"].FirstOrDefault();
        if (!_tokenService.ValidateAppToken(appToken))
        {
            return new UnauthorizedObjectResult(new { error = "Invalid extension token." });
        }

        // Extract user's Azure DevOps access token
        var userToken = TokenValidationService.ExtractBearerToken(
            req.Headers.Authorization.FirstOrDefault());

        if (string.IsNullOrEmpty(userToken))
        {
            return new UnauthorizedObjectResult(new { error = "Missing Authorization bearer token." });
        }

        // Rate limit check
        var rateLimited = CheckRateLimit(userToken);
        if (rateLimited is not null) return rateLimited;

        // Parse request body
        ChatRequest? chatRequest;
        try
        {
            chatRequest = await req.ReadFromJsonAsync<ChatRequest>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid request body");
            return new BadRequestObjectResult(new { error = "Invalid JSON request body." });
        }

        if (chatRequest is null || string.IsNullOrWhiteSpace(chatRequest.Message))
        {
            return new BadRequestObjectResult(new { error = "Message is required." });
        }

        // Process through agent orchestrator
        var response = await _orchestrator.ProcessMessageAsync(chatRequest, userToken);

        return new OkObjectResult(response);
    }

    /// <summary>
    /// POST /api/chat/stream — streaming chat endpoint returning Server-Sent Events.
    /// </summary>
    [Function("ChatStream")]
    public async Task<IActionResult> ChatStream(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat/stream")] HttpRequest req)
    {
        _logger.LogInformation("Streaming chat request received");

        // Validate tokens
        var appToken = req.Headers["X-Extension-Token"].FirstOrDefault();
        if (!_tokenService.ValidateAppToken(appToken))
        {
            return new UnauthorizedObjectResult(new { error = "Invalid extension token." });
        }

        var userToken = TokenValidationService.ExtractBearerToken(
            req.Headers.Authorization.FirstOrDefault());

        if (string.IsNullOrEmpty(userToken))
        {
            return new UnauthorizedObjectResult(new { error = "Missing Authorization bearer token." });
        }

        // Rate limit check
        var rateLimited = CheckRateLimit(userToken);
        if (rateLimited is not null) return rateLimited;

        ChatRequest? chatRequest;
        try
        {
            chatRequest = await req.ReadFromJsonAsync<ChatRequest>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid request body");
            return new BadRequestObjectResult(new { error = "Invalid JSON request body." });
        }

        if (chatRequest is null || string.IsNullOrWhiteSpace(chatRequest.Message))
        {
            return new BadRequestObjectResult(new { error = "Message is required." });
        }

        // Stream response via SSE
        req.HttpContext.Response.ContentType = "text/event-stream";
        req.HttpContext.Response.Headers.CacheControl = "no-cache";
        req.HttpContext.Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var streamEvent in _orchestrator.ProcessMessageStreamingAsync(chatRequest, userToken))
            {
                var sseData = $"data: {JsonSerializer.Serialize(streamEvent)}\n\n";
                await req.HttpContext.Response.WriteAsync(sseData);
                await req.HttpContext.Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming response");
            var errorEvent = new Models.StreamEvent { Type = "error", Content = ex.Message };
            var errorData = $"data: {JsonSerializer.Serialize(errorEvent)}\n\n";
            await req.HttpContext.Response.WriteAsync(errorData);
            await req.HttpContext.Response.Body.FlushAsync();
        }

        return new EmptyResult();
    }

    public void Dispose()
    {
        foreach (var limiter in _rateLimiters.Values)
            limiter.Dispose();
        _rateLimiters.Clear();
    }
}
