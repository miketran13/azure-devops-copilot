namespace DevOpsCopilot.Models.Configuration;

/// <summary>
/// Root configuration for MCP (Model Context Protocol) server connections.
/// Loaded from Config/mcp.json.
/// </summary>
public sealed class McpConfiguration
{
    public List<McpServerConfig> McpServers { get; set; } = new();
}

/// <summary>
/// Configuration for a single MCP server connection.
/// </summary>
public sealed class McpServerConfig
{
    /// <summary>
    /// Friendly name for the MCP server.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Server endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Transport type: "sse" or "stdio".
    /// </summary>
    public string Transport { get; set; } = "sse";

    /// <summary>
    /// Whether this server connection is active.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Authentication configuration for the MCP server.
    /// </summary>
    public McpAuthConfig? Authentication { get; set; }

    /// <summary>
    /// Which agent(s) should receive tools from this MCP server.
    /// Empty means all agents.
    /// </summary>
    public List<string> AgentAssignments { get; set; } = new();
}

/// <summary>
/// Authentication configuration for an MCP server.
/// </summary>
public sealed class McpAuthConfig
{
    /// <summary>
    /// Auth type: "none", "bearer", "apiKey".
    /// </summary>
    public string Type { get; set; } = "none";

    /// <summary>
    /// Token source — either a literal value or env var reference (e.g., "env:MCP_TOKEN").
    /// </summary>
    public string? TokenSource { get; set; }
}
