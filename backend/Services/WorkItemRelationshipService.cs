using System.Text.Json;
using DevOpsCopilot.Models;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Services;

/// <summary>
/// Handles composite parent-child workflows:
/// - Read parent context before creating children
/// - Create + link in a single workflow
/// - Duplicate detection via existing linked items
/// </summary>
public sealed class WorkItemRelationshipService
{
    private readonly AzureDevOpsService _devOps;
    private readonly ILogger<WorkItemRelationshipService> _logger;

    public WorkItemRelationshipService(AzureDevOpsService devOps, ILogger<WorkItemRelationshipService> logger)
    {
        _devOps = devOps;
        _logger = logger;
    }

    /// <summary>
    /// Reads the full context of a parent work item, including its
    /// title, description, acceptance criteria, area/iteration paths,
    /// tags, and existing child items.
    /// </summary>
    public async Task<ParentContext> GetParentContextAsync(int parentId, string project)
    {
        _logger.LogInformation("Reading parent context for work item #{ParentId}", parentId);

        var parent = await _devOps.GetWorkItemAsync(parentId)
            ?? throw new InvalidOperationException($"Parent work item #{parentId} not found.");

        var existingChildren = await GetExistingChildrenAsync(parentId, project);
        var links = await _devOps.GetWorkItemLinksAsync(parentId);

        return new ParentContext
        {
            Parent = parent,
            ExistingChildren = existingChildren,
            AllLinks = links,
        };
    }

    /// <summary>
    /// Creates a child work item and links it to the parent in a single workflow.
    /// Inherits area/iteration paths from the parent unless overridden.
    /// </summary>
    public async Task<WorkItemSummary> CreateChildAndLinkAsync(
        int parentId,
        string project,
        string workItemType,
        string title,
        string? description = null,
        string? assignedTo = null,
        string? areaPath = null,
        string? iterationPath = null,
        string? tags = null,
        int? priority = null,
        double? storyPoints = null,
        string? valueArea = null)
    {
        _logger.LogInformation("Creating child {Type} under parent #{ParentId}", workItemType, parentId);

        // Read parent to inherit context if not explicitly provided
        var parent = await _devOps.GetWorkItemAsync(parentId);
        if (parent is not null)
        {
            areaPath ??= parent.AreaPath;
            iterationPath ??= parent.IterationPath;
        }

        // Check for duplicate titles among existing children
        var existingChildren = await GetExistingChildrenAsync(parentId, project);
        var duplicate = existingChildren.FirstOrDefault(c =>
            string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));

        if (duplicate is not null)
        {
            _logger.LogWarning("Potential duplicate child detected: #{Id} '{Title}'",
                duplicate.Id, duplicate.Title);
            throw new InvalidOperationException(
                $"A child with a similar title already exists: #{duplicate.Id} \"{duplicate.Title}\". " +
                "Use a different title or confirm you want to create a duplicate.");
        }

        // Create the work item
        var child = await _devOps.CreateWorkItemAsync(
            project, workItemType, title,
            description, assignedTo, areaPath, iterationPath,
            tags, priority, storyPoints, valueArea);

        // Link parent -> child
        await _devOps.AddWorkItemLinkAsync(parentId, child.Id,
            "System.LinkTypes.Hierarchy-Forward");

        _logger.LogInformation("Created child #{ChildId} and linked to parent #{ParentId}",
            child.Id, parentId);

        return child;
    }

    /// <summary>
    /// Gets existing children of a parent work item.
    /// </summary>
    private async Task<List<WorkItemSummary>> GetExistingChildrenAsync(int parentId, string project)
    {
        try
        {
            var whereClause = $"[System.Parent] = {parentId}";
            return await _devOps.SearchWorkItemsAsync(whereClause, project);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch existing children for #{ParentId}", parentId);
            return [];
        }
    }
}

/// <summary>
/// Context about a parent work item, including its existing children and relationships.
/// </summary>
public sealed class ParentContext
{
    public required WorkItemSummary Parent { get; init; }
    public List<WorkItemSummary> ExistingChildren { get; init; } = [];
    public List<object> AllLinks { get; init; } = [];
}
