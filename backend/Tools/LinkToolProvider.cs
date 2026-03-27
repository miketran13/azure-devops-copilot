using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for work item link/relation operations.
/// </summary>
public sealed class LinkToolProvider : IToolProvider
{
    public string ToolGroupName => "link";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new LinkTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.LinkWorkItems),
            AIFunctionFactory.Create(tools.GetWorkItemLinks),
        ];
    }
}
