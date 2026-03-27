using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for Azure DevOps Wiki operations.
/// </summary>
public sealed class WikiToolProvider : IToolProvider
{
    public string ToolGroupName => "wiki";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new WikiTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.GetWikis),
            AIFunctionFactory.Create(tools.GetWikiPage),
            AIFunctionFactory.Create(tools.CreateWikiPage),
            AIFunctionFactory.Create(tools.UpdateWikiPage),
            AIFunctionFactory.Create(tools.DeleteWikiPage),
        ];
    }
}
