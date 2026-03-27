using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for organization-level and cross-project operations.
/// </summary>
public sealed class OrgTools
{
    private readonly AzureDevOpsService _devOps;

    public OrgTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("List all teams in a project.")]
    public async Task<string> GetTeams(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var teams = await _devOps.GetTeamsAsync(project);
            if (teams.Count == 0)
                return $"No teams found in project '{project}'.";
            return JsonSerializer.Serialize(teams, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing teams: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List all service connections (endpoints) in a project. " +
        "Useful for checking which external services are connected and their status.")]
    public async Task<string> GetServiceConnections(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var connections = await _devOps.GetServiceConnectionsAsync(project);
            if (connections.Count == 0)
                return $"No service connections found in project '{project}'.";
            return JsonSerializer.Serialize(connections, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing service connections: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get the revision history of a work item showing who changed what and when. " +
        "Returns a timeline of all changes made to the work item.")]
    public async Task<string> GetWorkItemHistory(
        [Description("Work item ID")] int workItemId)
    {
        try
        {
            var history = await _devOps.GetWorkItemRevisionsAsync(workItemId);
            if (history.Count == 0)
                return $"No history found for work item {workItemId}.";
            return JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting work item history: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Search work items across multiple projects in the organization. " +
        "Use this for cross-project portfolio views and org-wide queries.")]
    public async Task<string> CrossProjectSearch(
        [Description("WIQL WHERE clause for cross-project search. " +
            "Include [System.TeamProject] to filter specific projects, " +
            "or omit it to search all projects.")] string wiqlWhereClause,
        [Description("Maximum number of results (default 50)")] int top = 50)
    {
        try
        {
            var items = await _devOps.CrossProjectSearchAsync(wiqlWhereClause, top);
            if (items.Count == 0)
                return "No work items found matching the cross-project query.";
            return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR in cross-project search: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
