namespace DevOpsCopilot.Models.Configuration;

/// <summary>
/// Root configuration for tool/skill and agent enable/disable.
/// Loaded from Config/tools.json.
/// </summary>
public sealed class ToolConfiguration
{
    public Dictionary<string, ToolGroupConfig> Tools { get; set; } = new();
    public Dictionary<string, AgentConfig> Agents { get; set; } = new();
}

/// <summary>
/// Configuration for a single tool group.
/// </summary>
public sealed class ToolGroupConfig
{
    public bool Enabled { get; set; } = true;
    public string AgentAssignment { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Configuration for an agent.
/// </summary>
public sealed class AgentConfig
{
    public bool Enabled { get; set; } = true;
    public string? Description { get; set; }
}
