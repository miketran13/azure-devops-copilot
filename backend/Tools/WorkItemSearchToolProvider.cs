using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for work item search and query operations.
/// </summary>
public sealed class WorkItemSearchToolProvider : IToolProvider
{
    public string ToolGroupName => "workItemSearch";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new WorkItemSearchTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.SearchWorkItems),
            AIFunctionFactory.Create(tools.GetWorkItem),
            AIFunctionFactory.Create(tools.GetWorkItemsByIds),
        ];
    }
}
