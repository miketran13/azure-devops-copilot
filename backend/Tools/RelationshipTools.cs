using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for composite parent-child workflows.
/// Creates child items and auto-links them to parents in a single operation.
/// </summary>
public sealed class RelationshipTools
{
    private readonly WorkItemRelationshipService _relationshipService;

    public RelationshipTools(WorkItemRelationshipService relationshipService)
    {
        _relationshipService = relationshipService;
    }

    [Description("Read the full context of a parent work item before creating children. " +
        "Returns the parent's details (title, description, acceptance criteria, area/iteration paths, tags) " +
        "and all existing child items. ALWAYS call this before creating child items to understand context " +
        "and avoid duplicates.")]
    public async Task<string> GetParentContext(
        [Description("The parent work item ID")] int parentId,
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var context = await _relationshipService.GetParentContextAsync(parentId, project);

            var result = new
            {
                parent = context.Parent,
                existingChildCount = context.ExistingChildren.Count,
                existingChildren = context.ExistingChildren.Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.WorkItemType,
                    c.State,
                    c.AssignedTo,
                }).ToList(),
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR reading parent context: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Create a child work item and automatically link it to a parent work item. " +
        "The child inherits the parent's area path and iteration path unless explicitly overridden. " +
        "Checks for duplicate children before creating. " +
        "Use GetParentContext first to understand the parent and existing children.")]
    public async Task<string> CreateChildWorkItem(
        [Description("Parent work item ID to link the new child to")] int parentId,
        [Description("Azure DevOps project name")] string project,
        [Description("Work item type: Task, User Story, Bug, Test Case, etc.")] string workItemType,
        [Description("Title of the child work item")] string title,
        [Description("HTML description (optional)")] string? description = null,
        [Description("Assigned to user (optional)")] string? assignedTo = null,
        [Description("Area path override (optional — inherits from parent if not set)")] string? areaPath = null,
        [Description("Iteration path override (optional — inherits from parent if not set)")] string? iterationPath = null,
        [Description("Semicolon-separated tags (optional)")] string? tags = null,
        [Description("Priority 1-4 (optional)")] int? priority = null,
        [Description("Story points / effort (optional)")] double? storyPoints = null,
        [Description("Value area: Business or Architectural (optional)")] string? valueArea = null)
    {
        try
        {
            var child = await _relationshipService.CreateChildAndLinkAsync(
                parentId, project, workItemType, title,
                description, assignedTo, areaPath, iterationPath,
                tags, priority, storyPoints, valueArea);

            return $"Successfully created {workItemType} #{child.Id}: \"{child.Title}\" and linked to parent #{parentId}.\n" +
                   JsonSerializer.Serialize(child, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR creating child work item: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
