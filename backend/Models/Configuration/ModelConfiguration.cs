namespace DevOpsCopilot.Models.Configuration;

/// <summary>
/// Configuration for Azure OpenAI models.
/// Loaded from app settings AzureOpenAI:Models array.
/// </summary>
public sealed class AzureOpenAIConfiguration
{
    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string DefaultDeployment { get; set; } = string.Empty;
    public List<ModelConfig> Models { get; set; } = new();
}

/// <summary>
/// A single model deployment available for selection.
/// </summary>
public sealed class ModelConfig
{
    /// <summary>
    /// Unique identifier for this model (sent from frontend).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI deployment name.
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name shown in the UI dropdown.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the model.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is the default model.
    /// </summary>
    public bool IsDefault { get; set; }
}
