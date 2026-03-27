using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for analyzing work items — assessing quality,
/// generating suggestions, test cases, and child item recommendations.
/// </summary>
public sealed class AnalysisTools
{
    private readonly AzureDevOpsService _devOps;

    public AnalysisTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("Fetch a work item and return its full details for analysis. " +
        "Use this to get the raw data before performing requirement analysis, " +
        "quality assessment, or generating suggestions.")]
    public async Task<string> GetWorkItemForAnalysis(
        [Description("The work item ID to analyze")] int workItemId)
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

    [Description("Search for child/related work items under a parent Feature or Epic. " +
        "Use this to understand the current decomposition before suggesting new child items.")]
    public async Task<string> GetChildWorkItems(
        [Description("The parent work item ID (Feature or Epic)")] int parentWorkItemId,
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var whereClause = $"[System.Parent] = {parentWorkItemId}";
            var children = await _devOps.SearchWorkItemsAsync(whereClause, project);
            if (children.Count == 0)
                return $"No child work items found under #{parentWorkItemId}.";
            return JsonSerializer.Serialize(children, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving children of #{parentWorkItemId}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get all work items in a specific iteration/sprint for context. " +
        "Useful for sprint analysis and capacity planning.")]
    public async Task<string> GetSprintWorkItems(
        [Description("Azure DevOps project name")] string project,
        [Description("Iteration path, e.g. 'MyProject\\Sprint 5'")] string iterationPath)
    {
        try
        {
            var whereClause = $"[System.IterationPath] = '{iterationPath}' " +
                              "AND [System.State] <> 'Removed'";
            var items = await _devOps.SearchWorkItemsAsync(whereClause, project, 100);
            if (items.Count == 0)
                return $"No work items found in iteration '{iterationPath}'.";
            return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving sprint '{iterationPath}': {ex.GetType().Name}: {ex.Message}";
        }
    }
}
