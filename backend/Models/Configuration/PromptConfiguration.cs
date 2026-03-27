namespace DevOpsCopilot.Models.Configuration;

/// <summary>
/// Root configuration for agent system prompts and suggested actions.
/// Loaded from Config/prompts.json — hot-reloadable via IOptionsMonitor.
/// </summary>
public sealed class PromptConfiguration
{
    public Dictionary<string, AgentPromptConfig> Agents { get; set; } = new();
    public SuggestedActionsConfig SuggestedActions { get; set; } = new();
}

/// <summary>
/// Per-agent prompt configuration.
/// </summary>
public sealed class AgentPromptConfig
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string? DefaultGreeting { get; set; }
    public int MaxTokens { get; set; } = 4096;
}

/// <summary>
/// Configuration for suggested follow-up actions.
/// </summary>
public sealed class SuggestedActionsConfig
{
    /// <summary>
    /// Keyword patterns (regex) mapped to suggested actions.
    /// Key is a regex pattern, value is a list of suggested action strings.
    /// </summary>
    public Dictionary<string, List<string>> Keywords { get; set; } = new();

    /// <summary>
    /// Default suggested actions when no keyword matches.
    /// </summary>
    public List<string> Defaults { get; set; } = new();
}
