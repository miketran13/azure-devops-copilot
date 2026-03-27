using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for repository browsing and code reading.
/// </summary>
public sealed class RepositoryToolProvider : IToolProvider
{
    public string ToolGroupName => "repository";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new RepositoryTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.ListRepositories),
            AIFunctionFactory.Create(tools.GetFileContent),
            AIFunctionFactory.Create(tools.GetDirectoryTree),
        ];
    }
}
