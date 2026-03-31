namespace DevOpsCopilot.Models.Configuration;

/// <summary>
/// Configuration for GitHub Models AI provider.
/// Loaded from app settings GitHubModels section.
/// </summary>
public sealed class GitHubModelsConfiguration
{
    /// <summary>
    /// GitHub Models inference endpoint. Defaults to the public endpoint.
    /// </summary>
    public string Endpoint { get; set; } = "https://models.github.ai/inference/";

    /// <summary>
    /// Server-side default API key (GitHub PAT with models:read scope).
    /// Can be overridden per-request in standalone mode via X-GitHub-Token header.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Default model to use when none is specified.
    /// </summary>
    public string DefaultModel { get; set; } = "openai/gpt-4o-mini";

    /// <summary>
    /// Available models for selection in the UI.
    /// Reuses ModelConfig from AzureOpenAIConfiguration.
    /// </summary>
    public List<ModelConfig> Models { get; set; } = new();
}
