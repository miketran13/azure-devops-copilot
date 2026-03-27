namespace DevOpsCopilot.Models.Configuration;

/// <summary>
/// Root configuration for session/memory storage.
/// Loaded from Config/memory.json.
/// </summary>
public sealed class MemoryConfiguration
{
    public MemorySettings Memory { get; set; } = new();
}

public sealed class MemorySettings
{
    /// <summary>
    /// Whether persistent session storage is enabled.
    /// When false, a NullSessionStore is used and no history is saved.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Storage provider: "inMemory", "azureTableStorage", "cosmosDb", "azureBlobStorage".
    /// </summary>
    public string Provider { get; set; } = "inMemory";

    /// <summary>
    /// Name of the connection string key (from app settings) used by the provider.
    /// </summary>
    public string ConnectionStringKey { get; set; } = "AzureWebJobsStorage";

    /// <summary>
    /// Number of days to retain sessions before auto-cleanup.
    /// </summary>
    public int SessionTtlDays { get; set; } = 30;

    /// <summary>
    /// Maximum number of sessions per user.
    /// </summary>
    public int MaxSessionsPerUser { get; set; } = 50;

    /// <summary>
    /// Maximum number of messages stored per session.
    /// </summary>
    public int MaxMessagesPerSession { get; set; } = 200;
}
