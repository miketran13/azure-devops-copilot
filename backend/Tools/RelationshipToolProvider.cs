using DevOpsCopilot.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for parent-child relationship operations.
/// Provides tools for reading parent context and creating auto-linked child items.
/// </summary>
public sealed class RelationshipToolProvider : IToolProvider
{
    public string ToolGroupName => "relationship";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var relationshipService = new WorkItemRelationshipService(
            devOpsService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkItemRelationshipService>.Instance);
        var tools = new RelationshipTools(relationshipService);
        return
        [
            AIFunctionFactory.Create(tools.GetParentContext),
            AIFunctionFactory.Create(tools.CreateChildWorkItem),
        ];
    }
}
