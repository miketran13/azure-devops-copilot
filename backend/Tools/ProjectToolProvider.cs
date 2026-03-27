using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for project-level metadata queries.
/// </summary>
public sealed class ProjectToolProvider : IToolProvider
{
    public string ToolGroupName => "project";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new ProjectTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.GetWorkItemTypes),
            AIFunctionFactory.Create(tools.ListProjects),
            AIFunctionFactory.Create(tools.GetIterations),
            AIFunctionFactory.Create(tools.GetAreaPaths),
            AIFunctionFactory.Create(tools.GetTeamMembers),
        ];
    }
}
