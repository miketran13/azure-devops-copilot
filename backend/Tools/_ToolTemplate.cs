// ═══════════════════════════════════════════════════════════════════
// TOOL TEMPLATE — Copy this file to create a new tool provider
// ═══════════════════════════════════════════════════════════════════
//
// Steps to add a new tool:
// 1. Copy this file and rename it (e.g. MyNewTools.cs + MyNewToolProvider.cs)
// 2. Implement your tool methods with [Description] attributes
// 3. Create a matching IToolProvider class
// 4. Add an entry in Config/tools.json:
//    "myNewTool": { "enabled": true, "agentAssignment": "search" }
// 5. Build and run — the tool is auto-discovered and registered!
//
// No changes to AgentFactory.cs or Program.cs are needed.
// ═══════════════════════════════════════════════════════════════════

using System.ComponentModel;
using DevOpsCopilot.Services;
using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Tools;

/*
/// <summary>
/// AI-callable tool functions for [describe what your tools do].
/// </summary>
public sealed class MyNewTools
{
    private readonly AzureDevOpsService _devOps;

    public MyNewTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("Description of what this tool does — the LLM reads this to decide when to call it.")]
    public async Task<string> MyToolMethod(
        [Description("Description of this parameter")] string param1,
        [Description("Optional parameter")] string? param2 = null)
    {
        try
        {
            // Call _devOps methods or implement your logic
            return "Success message with results";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }
}

/// <summary>
/// Tool provider that registers MyNewTools with the agent system.
/// </summary>
public sealed class MyNewToolProvider : IToolProvider
{
    // Must match the key in Config/tools.json
    public string ToolGroupName => "myNewTool";

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new MyNewTools(devOpsService);
        return
        [
            AIFunctionFactory.Create(tools.MyToolMethod),
        ];
    }
}
*/
