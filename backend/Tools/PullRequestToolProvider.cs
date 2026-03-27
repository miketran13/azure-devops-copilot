using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Tool provider for pull request operations.
/// </summary>
public sealed class PullRequestToolProvider : IToolProvider
{
    public string ToolGroupName => "pullRequest";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new PullRequestTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.CreatePullRequest),
            AIFunctionFactory.Create(tools.UpdatePullRequest),
            AIFunctionFactory.Create(tools.GetPullRequest),
            AIFunctionFactory.Create(tools.SearchPullRequests),
            AIFunctionFactory.Create(tools.CompletePullRequest),
        ];
    }
}
