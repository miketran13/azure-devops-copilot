using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using DevOpsCopilot.Models.Configuration;

namespace DevOpsCopilot.Services.Providers;

/// <summary>
/// Provides AI chat clients backed by GitHub Models (https://models.github.ai).
/// Uses the standard OpenAI SDK with a custom endpoint.
/// Supports a server-side default API key or per-request override via SetRequestApiKey().
/// </summary>
public sealed class GitHubModelsChatClientProvider : IChatClientProvider
{
    private readonly GitHubModelsConfiguration _config;
    private readonly ILogger<GitHubModelsChatClientProvider> _logger;

    // Thread-local per-request API key override for standalone mode
    private static readonly AsyncLocal<string?> _requestApiKey = new();

    public GitHubModelsChatClientProvider(
        IOptions<GitHubModelsConfiguration> config,
        ILogger<GitHubModelsChatClientProvider> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public IChatClient GetChatClient(string modelOrDeployment)
    {
        var apiKey = _requestApiKey.Value ?? _config.ApiKey
            ?? throw new InvalidOperationException(
                "No GitHub Models API key configured. Set GitHubModels:ApiKey in app settings or provide X-GitHub-Token header.");

        var endpoint = _config.Endpoint.TrimEnd('/');

        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);

        _logger.LogDebug("Creating GitHub Models chat client for model '{Model}' at '{Endpoint}'",
            modelOrDeployment, endpoint);

        return client
            .GetChatClient(modelOrDeployment)
            .AsIChatClient();
    }

    /// <summary>
    /// Set a per-request API key override (for standalone mode where users provide their own PAT).
    /// Call this before processing a request; it flows with the async context.
    /// </summary>
    public static void SetRequestApiKey(string? apiKey)
    {
        _requestApiKey.Value = apiKey;
    }

    /// <summary>
    /// Clear the per-request API key override after request processing.
    /// </summary>
    public static void ClearRequestApiKey()
    {
        _requestApiKey.Value = null;
    }
}
