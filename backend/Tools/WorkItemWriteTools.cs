using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Models;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for creating and updating Azure DevOps work items.
/// </summary>
public sealed class WorkItemWriteTools
{
    private readonly AzureDevOpsService _devOps;
    private readonly MappingService _mappingService;

    public WorkItemWriteTools(AzureDevOpsService devOps, MappingService mappingService)
    {
        _devOps = devOps;
        _mappingService = mappingService;
    }

    /// <summary>
    /// Resolves user-supplied field names to ADO reference names using MappingService.
    /// Returns a warning message if any field has low confidence, or null if all are OK.
    /// </summary>
    private (Dictionary<string, string> resolved, string? warning) ResolveFieldUpdates(
        Dictionary<string, string> rawFields, string? workItemType = null)
    {
        var resolved = new Dictionary<string, string>();
        var warnings = new List<string>();

        foreach (var (key, value) in rawFields)
        {
            // Well-known system fields pass through directly
            if (key.StartsWith("System.") || key.StartsWith("Microsoft.VSTS."))
            {
                resolved[key] = value;
                continue;
            }

            var match = _mappingService.ResolveFieldName(key, workItemType);
            if (match.IsMatch && match.Field is not null)
            {
                if (MappingService.RequiresConfirmation(match))
                {
                    warnings.Add(
                        $"Field '{key}' was fuzzy-matched to '{match.Field.DisplayName}' " +
                        $"({match.Field.ReferenceName}) with {match.Confidence:P0} confidence. " +
                        "Please confirm this is correct.");
                }
                resolved[match.Field.ReferenceName] = value;
            }
            else
            {
                // No match found — pass through as-is (ADO will reject if invalid)
                resolved[key] = value;
            }
        }

        return (resolved, warnings.Count > 0 ? string.Join("\n", warnings) : null);
    }

    [Description("Create a new work item in Azure DevOps. " +
        "Supported types: Bug, Task, User Story, Feature, Epic, Test Case, Issue. " +
        "Execute immediately when called — confirmation is handled by the orchestrator.")]
    public async Task<string> CreateWorkItem(
        [Description("Azure DevOps project name")] string project,
        [Description("Work item type: Bug, Task, User Story, Feature, Epic, Test Case, Issue")] string workItemType,
        [Description("Title of the work item")] string title,
        [Description("HTML description of the work item (optional)")] string? description = null,
        [Description("Assigned to user (display name or email, optional)")] string? assignedTo = null,
        [Description("Area path (optional, defaults to project root)")] string? areaPath = null,
        [Description("Iteration path / sprint (optional)")] string? iterationPath = null,
        [Description("Semicolon-separated tags (optional)")] string? tags = null,
        [Description("Priority 1-4 where 1 is highest (optional)")] int? priority = null,
        [Description("Story points / effort estimate (optional, numeric)")] double? storyPoints = null,
        [Description("Value area: Business or Architectural (optional)")] string? valueArea = null)
    {
        try
        {
            var created = await _devOps.CreateWorkItemAsync(
                project, workItemType, title,
                description, assignedTo, areaPath, iterationPath, tags, priority,
                storyPoints, valueArea);

            return $"Successfully created {workItemType} #{created.Id}: \"{created.Title}\"\n" +
                   JsonSerializer.Serialize(created, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR creating work item: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Update fields on an existing Azure DevOps work item. " +
        "Only provide the fields you want to change — omit fields that should stay the same. " +
        "Execute immediately when called — confirmation is handled by the orchestrator.")]
    public async Task<string> UpdateWorkItem(
        [Description("The work item ID to update")] int workItemId,
        [Description("New state: New, Active, Resolved, Closed, Removed (optional)")] string? state = null,
        [Description("New title (optional)")] string? title = null,
        [Description("New assigned user - display name or email (optional)")] string? assignedTo = null,
        [Description("New HTML description (optional)")] string? description = null,
        [Description("New acceptance criteria in HTML (optional, for User Stories)")] string? acceptanceCriteria = null,
        [Description("New area path (optional)")] string? areaPath = null,
        [Description("New iteration path / sprint (optional)")] string? iterationPath = null,
        [Description("New semicolon-separated tags (optional)")] string? tags = null,
        [Description("New priority 1-4 where 1 is highest (optional)")] int? priority = null,
        [Description("New story points / effort estimate (optional, numeric)")] double? storyPoints = null,
        [Description("New value area: Business or Architectural (optional)")] string? valueArea = null,
        [Description("New comment / discussion entry to add (optional, HTML supported)")] string? comment = null)
    {
        var fieldUpdates = new Dictionary<string, string>();
        if (state is not null) fieldUpdates["System.State"] = state;
        if (title is not null) fieldUpdates["System.Title"] = title;
        if (assignedTo is not null) fieldUpdates["System.AssignedTo"] = assignedTo;
        if (description is not null) fieldUpdates["System.Description"] = description;
        if (acceptanceCriteria is not null) fieldUpdates["Microsoft.VSTS.Common.AcceptanceCriteria"] = acceptanceCriteria;
        if (areaPath is not null) fieldUpdates["System.AreaPath"] = areaPath;
        if (iterationPath is not null) fieldUpdates["System.IterationPath"] = iterationPath;
        if (tags is not null) fieldUpdates["System.Tags"] = tags;
        if (priority.HasValue) fieldUpdates["Microsoft.VSTS.Common.Priority"] = priority.Value.ToString();
        if (storyPoints.HasValue) fieldUpdates["Microsoft.VSTS.Scheduling.StoryPoints"] = storyPoints.Value.ToString();
        if (valueArea is not null) fieldUpdates["Microsoft.VSTS.Common.ValueArea"] = valueArea;
        if (comment is not null) fieldUpdates["System.History"] = comment;

        if (fieldUpdates.Count == 0)
            return "Error: No field updates provided. Specify at least one field to change.";

        // Resolve field names through MappingService for best-guess matching
        var (resolved, warning) = ResolveFieldUpdates(fieldUpdates);

        if (warning is not null)
            return $"⚠️ Field mapping requires confirmation:\n{warning}\n\n" +
                   "**Actions:** `Yes, apply these mappings` · `No, cancel the update`";

        try
        {
            var updated = await _devOps.UpdateWorkItemAsync(workItemId, resolved);
            return $"Successfully updated work item #{updated.Id}: \"{updated.Title}\"\n" +
                   JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR updating work item {workItemId}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Create multiple work items in a single batch. " +
        "Each item is a JSON object with required fields 'type' and 'title', " +
        "plus optional fields: description, assignedTo, areaPath, iterationPath, tags, priority, storyPoints, valueArea. " +
        "Returns a summary of all created items. Use this when the user asks to create several work items at once.")]
    public async Task<string> BatchCreateWorkItems(
        [Description("Azure DevOps project name")] string project,
        [Description("JSON array of work items, e.g. " +
            "[{\"type\":\"Task\",\"title\":\"Setup CI\"},{\"type\":\"Bug\",\"title\":\"Fix login\"}]")]
        string workItemsJson)
    {
        List<Dictionary<string, object>>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(workItemsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            return $"ERROR: Invalid JSON array: {ex.Message}";
        }

        if (items is null || items.Count == 0)
            return "ERROR: No work items provided. Supply a JSON array with at least one item.";

        if (items.Count > 20)
            return "ERROR: Batch size limited to 20 items. Please split into smaller batches.";

        var results = new List<string>();
        var created = 0;
        var failed = 0;

        foreach (var item in items)
        {
            string type = item.TryGetValue("type", out var t) ? t.ToString()! : "";
            string title = item.TryGetValue("title", out var ti) ? ti.ToString()! : "";

            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(title))
            {
                failed++;
                results.Add($"❌ Skipped: missing required 'type' or 'title'");
                continue;
            }

            try
            {
                string? Str(string key) => item.TryGetValue(key, out var v) ? v.ToString() : null;
                int? Int(string key) => item.TryGetValue(key, out var v) && int.TryParse(v.ToString(), out var i) ? i : null;
                double? Dbl(string key) => item.TryGetValue(key, out var v) && double.TryParse(v.ToString(), out var d) ? d : null;

                var wi = await _devOps.CreateWorkItemAsync(
                    project, type, title,
                    Str("description"), Str("assignedTo"), Str("areaPath"),
                    Str("iterationPath"), Str("tags"), Int("priority"),
                    Dbl("storyPoints"), Str("valueArea"));

                created++;
                results.Add($"✅ {type} #{wi.Id}: \"{wi.Title}\"");
            }
            catch (Exception ex)
            {
                failed++;
                results.Add($"❌ {type} \"{title}\": {ex.Message}");
            }
        }

        return $"Batch complete: {created} created, {failed} failed.\n\n" +
               string.Join("\n", results);
    }

    [Description("Add a comment/discussion entry to an existing work item. " +
        "The comment supports HTML. " +
        "IMPORTANT: When mentioning a user (e.g. @Name), you MUST format it as the ADO HTML mention tag: " +
        "<a href=\"#\" data-vss-mention=\"version:2.0,{identity_id}\">@Display Name</a> " +
        "where {identity_id} is the user's Azure DevOps identity GUID provided in [Team members: Name (identity_id: guid)] context. " +
        "Plain text @mentions do NOT trigger notifications — the HTML format is required.")]
    public async Task<string> AddComment(
        [Description("The work item ID")] int workItemId,
        [Description("The comment text to add (HTML supported; use ADO mention tags for @mentions)")] string comment)
    {
        var fieldUpdates = new Dictionary<string, string>
        {
            ["System.History"] = comment
        };

        try
        {
            var updated = await _devOps.UpdateWorkItemAsync(workItemId, fieldUpdates);
            return $"Comment added to work item #{updated.Id}.";
        }
        catch (Exception ex)
        {
            return $"ERROR adding comment to work item {workItemId}: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
