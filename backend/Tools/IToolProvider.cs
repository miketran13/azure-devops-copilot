using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// Interface for pluggable tool providers. Implement this to add new tool groups
/// that are auto-discovered and registered with agents at startup.
///
/// To create a new tool provider:
/// 1. Create a class implementing IToolProvider
/// 2. Return your AIFunction instances from GetTools()
/// 3. Set ToolGroupName to match its key in Config/tools.json
/// 4. Add an entry in Config/tools.json with enabled + agentAssignment
///
/// The system will auto-discover all IToolProvider implementations in the assembly.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Unique name matching the key in Config/tools.json (e.g. "workItemSearch").
    /// </summary>
    string ToolGroupName { get; }

    /// <summary>
    /// Create and return the AI-callable tool functions for this group.
    /// Called per-request with the user's scoped AzureDevOpsService.
    /// </summary>
    IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService);
}
