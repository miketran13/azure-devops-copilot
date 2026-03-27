using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for managing work item links and relations.
/// </summary>
public sealed class LinkTools
{
    private readonly AzureDevOpsService _devOps;

    public LinkTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("Link two work items together with a specified relationship type. " +
        "Common link types: " +
        "parent-child (parent->child), " +
        "child-parent (child->parent), " +
        "related, " +
        "tested-by (work item is tested by a Test Case), " +
        "tests (Test Case tests a work item), " +
        "affects (Bug affects a work item), " +
        "affected-by (work item is affected by a Bug).")]
    public async Task<string> LinkWorkItems(
        [Description("Source work item ID")] int sourceId,
        [Description("Target work item ID")] int targetId,
        [Description("Link type: 'parent-child', 'child-parent', 'related', " +
            "'tested-by', 'tests', 'affects', 'affected-by' " +
            "(default: 'parent-child')")] string linkType = "parent-child")
    {
        try
        {
            var adoLinkType = linkType.ToLowerInvariant() switch
            {
                "parent-child" or "hierarchy-forward" => "System.LinkTypes.Hierarchy-Forward",
                "child-parent" or "hierarchy-reverse" => "System.LinkTypes.Hierarchy-Reverse",
                "related" => "System.LinkTypes.Related",
                "tested-by" => "Microsoft.VSTS.Common.TestedBy-Forward",
                "tests" => "Microsoft.VSTS.Common.TestedBy-Reverse",
                "affects" => "Microsoft.VSTS.Common.Affects-Forward",
                "affected-by" => "Microsoft.VSTS.Common.Affects-Reverse",
                _ => linkType // Allow raw ADO link type reference names
            };

            var result = await _devOps.AddWorkItemLinkAsync(sourceId, targetId, adoLinkType);
            return $"Successfully linked work item #{sourceId} → #{targetId} ({linkType}).\n" +
                   JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR linking {sourceId} → {targetId}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get all links/relations of a work item. " +
        "Returns parent, child, related, and other link types.")]
    public async Task<string> GetWorkItemLinks(
        [Description("Work item ID")] int workItemId)
    {
        try
        {
            var links = await _devOps.GetWorkItemLinksAsync(workItemId);
            if (links.Count == 0)
                return $"Work item #{workItemId} has no links.";
            return $"Links for #{workItemId} ({links.Count}):\n" +
                   JsonSerializer.Serialize(links, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting links for #{workItemId}: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
