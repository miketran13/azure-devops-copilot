using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Models;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for searching and reading Azure DevOps work items.
/// These methods are wrapped via AIFunctionFactory.Create() and exposed to the LLM.
/// </summary>
public sealed class WorkItemSearchTools
{
    private readonly AzureDevOpsService _devOps;

    public WorkItemSearchTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("Search Azure DevOps work items using a WIQL WHERE clause. " +
        "Use standard WIQL syntax for the WHERE clause. " +
        "Common fields: [System.WorkItemType], [System.State], [System.AssignedTo], " +
        "[System.Title], [System.Tags], [System.AreaPath], [System.IterationPath]. " +
        "Common operators: =, <>, CONTAINS, IN, UNDER. " +
        "Use @Me for current user, @Today for today's date, @Project for current project.")]
    public async Task<string> SearchWorkItems(
        [Description("WIQL WHERE clause, e.g. \"[System.WorkItemType] = 'Bug' AND [System.State] = 'Active'\"")] string whereClause,
        [Description("Azure DevOps project name")] string project,
        [Description("Maximum number of results to return (default 20)")] int top = 20)
    {
        try
        {
            var items = await _devOps.SearchWorkItemsAsync(whereClause, project, top);
            if (items.Count == 0)
                return "No work items found matching the query.";
            return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR calling Azure DevOps API: {ex.GetType().Name}: {ex.Message}. " +
                   $"Check that the project name '{project}' is correct and you have access. " +
                   $"WIQL clause was: {whereClause}";
        }
    }

    [Description("Get a single Azure DevOps work item by its ID. Returns all fields including description.")]
    public async Task<string> GetWorkItem(
        [Description("The work item ID (integer)")] int workItemId)
    {
        try
        {
            var item = await _devOps.GetWorkItemAsync(workItemId);
            if (item is null)
                return $"Work item {workItemId} not found.";
            return JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving work item {workItemId}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get multiple Azure DevOps work items by their IDs. Useful for batch retrieval.")]
    public async Task<string> GetWorkItemsByIds(
        [Description("Comma-separated list of work item IDs, e.g. '101,102,103'")] string workItemIds)
    {
        try
        {
            var ids = workItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : -1)
                .Where(id => id > 0)
                .ToArray();

            if (ids.Length == 0)
                return "No valid work item IDs provided.";

            var items = await _devOps.GetWorkItemsByIdsAsync(ids);
            return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving work items '{workItemIds}': {ex.GetType().Name}: {ex.Message}";
        }
    }
}
