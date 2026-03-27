using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for work item create/update/comment operations.
/// </summary>
public sealed class WorkItemWriteToolProvider : IToolProvider
{
    private readonly MappingService _mappingService;

    public WorkItemWriteToolProvider(MappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public string ToolGroupName => "workItemWrite";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new WorkItemWriteTools(devOpsService, _mappingService);
        return
        [
            AIFunctionFactory.Create(tools.CreateWorkItem),
            AIFunctionFactory.Create(tools.UpdateWorkItem),
            AIFunctionFactory.Create(tools.BatchCreateWorkItems),
            AIFunctionFactory.Create(tools.AddComment),
        ];
    }
}
