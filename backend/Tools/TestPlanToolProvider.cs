using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for Azure DevOps Test Plan operations.
/// </summary>
public sealed class TestPlanToolProvider : IToolProvider
{
    public string ToolGroupName => "testPlan";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new TestPlanTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.GetTestPlans),
            AIFunctionFactory.Create(tools.GetTestSuites),
            AIFunctionFactory.Create(tools.GetTestCases),
            AIFunctionFactory.Create(tools.GetTestRuns),
            AIFunctionFactory.Create(tools.GetTestResults),
            AIFunctionFactory.Create(tools.GetCodeCoverage),
        ];
    }
}
