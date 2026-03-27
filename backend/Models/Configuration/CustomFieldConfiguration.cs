namespace DevOpsCopilot.Models.Configuration;

/// <summary>
/// Root configuration for custom field mappings.
/// Loaded from Config/custom-fields.json.
/// </summary>
public sealed class CustomFieldConfiguration
{
    public List<CustomFieldMapping> FieldMappings { get; set; } = new();
    public bool AutoDiscoverFields { get; set; }
    public List<string> ExcludeFieldPrefixes { get; set; } = new();
}

/// <summary>
/// Maps a custom Azure DevOps field to a short name for tool/LLM usage.
/// </summary>
public sealed class CustomFieldMapping
{
    /// <summary>
    /// ADO reference name, e.g. "Custom.BusinessValue" or "Microsoft.VSTS.Scheduling.StoryPoints".
    /// </summary>
    public string ReferenceName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Short name used in tool parameters and LLM interactions.
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Data type: "string", "integer", "double", "boolean", "datetime".
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// Work item types this field applies to. Empty means all types.
    /// </summary>
    public List<string> WorkItemTypes { get; set; } = new();

    /// <summary>
    /// Whether to include this field when fetching work items for search results.
    /// </summary>
    public bool IncludeInSearch { get; set; }

    /// <summary>
    /// Whether to include this field in display/card views.
    /// </summary>
    public bool IncludeInDisplay { get; set; }
}
