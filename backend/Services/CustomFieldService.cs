using DevOpsCopilot.Models.Configuration;
using Microsoft.Extensions.Options;

namespace DevOpsCopilot.Services;

/// <summary>
/// Provides access to custom field mappings configured in Config/custom-fields.json.
/// </summary>
public sealed class CustomFieldService
{
    private readonly IOptionsMonitor<CustomFieldConfiguration> _options;

    public CustomFieldService(IOptionsMonitor<CustomFieldConfiguration> options)
    {
        _options = options;
    }

    /// <summary>
    /// Get all configured custom field mappings.
    /// </summary>
    public IReadOnlyList<CustomFieldMapping> GetConfiguredFields()
        => _options.CurrentValue.FieldMappings.AsReadOnly();

    /// <summary>
    /// Get custom fields applicable to a specific work item type.
    /// Returns all fields if the field has no WorkItemTypes restriction.
    /// </summary>
    public IReadOnlyList<CustomFieldMapping> GetFieldsForWorkItemType(string workItemType)
        => _options.CurrentValue.FieldMappings
            .Where(f => f.WorkItemTypes.Count == 0
                     || f.WorkItemTypes.Contains(workItemType, StringComparer.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Get the ADO reference names for all fields that should be included in search/batch queries.
    /// </summary>
    public IReadOnlyList<string> GetSearchFieldReferenceNames()
        => _options.CurrentValue.FieldMappings
            .Where(f => f.IncludeInSearch)
            .Select(f => f.ReferenceName)
            .ToList()
            .AsReadOnly();

    /// <summary>
    /// Whether auto-discovery of fields from the ADO API is enabled.
    /// </summary>
    public bool IsAutoDiscoverEnabled => _options.CurrentValue.AutoDiscoverFields;

    /// <summary>
    /// Field prefixes to exclude from auto-discovery.
    /// </summary>
    public IReadOnlyList<string> ExcludeFieldPrefixes
        => _options.CurrentValue.ExcludeFieldPrefixes.AsReadOnly();
}
