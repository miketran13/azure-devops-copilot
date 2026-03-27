using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for pipeline, build, release, and variable group operations.
/// </summary>
public sealed class PipelineToolProvider : IToolProvider
{
    public string ToolGroupName => "pipeline";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new PipelineTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.GetPipelines),
            AIFunctionFactory.Create(tools.GetPipeline),
            AIFunctionFactory.Create(tools.RunPipeline),
            AIFunctionFactory.Create(tools.GetPipelineRuns),
            AIFunctionFactory.Create(tools.GetBuildLogs),
            AIFunctionFactory.Create(tools.GetBuildTimeline),
            AIFunctionFactory.Create(tools.CancelBuild),
            AIFunctionFactory.Create(tools.GetBuildArtifacts),
            AIFunctionFactory.Create(tools.GetVariableGroups),
            AIFunctionFactory.Create(tools.AddPipelineVariable),
            AIFunctionFactory.Create(tools.GetAgentPools),
            AIFunctionFactory.Create(tools.GetFailedTestResults),
        ];
    }
}
