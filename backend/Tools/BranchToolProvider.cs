using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for Git branch operations.
/// </summary>
public sealed class BranchToolProvider : IToolProvider
{
    public string ToolGroupName => "branch";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new BranchTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.CreateBranch),
            AIFunctionFactory.Create(tools.ListBranches),
        ];
    }
}
