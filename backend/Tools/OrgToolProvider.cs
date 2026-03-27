using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for organization-level and cross-project operations.
/// </summary>
public sealed class OrgToolProvider : IToolProvider
{
    public string ToolGroupName => "org";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new OrgTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.GetTeams),
            AIFunctionFactory.Create(tools.GetServiceConnections),
            AIFunctionFactory.Create(tools.GetWorkItemHistory),
            AIFunctionFactory.Create(tools.CrossProjectSearch),
        ];
    }
}
