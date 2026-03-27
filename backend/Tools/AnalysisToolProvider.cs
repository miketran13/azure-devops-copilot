using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for requirement analysis, test case generation, and sprint analysis.
/// </summary>
public sealed class AnalysisToolProvider : IToolProvider
{
    public string ToolGroupName => "analysis";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new AnalysisTools(devOpsService);
        var searchTools = new WorkItemSearchTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.GetWorkItemForAnalysis),
            AIFunctionFactory.Create(tools.GetChildWorkItems),
            AIFunctionFactory.Create(tools.GetSprintWorkItems),
            AIFunctionFactory.Create(searchTools.SearchWorkItems),
        ];
    }
}
