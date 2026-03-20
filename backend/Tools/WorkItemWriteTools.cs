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

    public WorkItemWriteTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
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
        [Description("Priority 1-4 where 1 is highest (optional)")] int? priority = null)
    {
        try
        {
            var created = await _devOps.CreateWorkItemAsync(
                project, workItemType, title,
                description, assignedTo, areaPath, iterationPath, tags, priority);

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
        if (comment is not null) fieldUpdates["System.History"] = comment;

        if (fieldUpdates.Count == 0)
            return "Error: No field updates provided. Specify at least one field to change.";

        try
        {
            var updated = await _devOps.UpdateWorkItemAsync(workItemId, fieldUpdates);
            return $"Successfully updated work item #{updated.Id}: \"{updated.Title}\"\n" +
                   JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR updating work item {workItemId}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Add a comment/discussion entry to an existing work item.")]
    public async Task<string> AddComment(
        [Description("The work item ID")] int workItemId,
        [Description("The comment text to add (HTML supported)")] string comment)
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
