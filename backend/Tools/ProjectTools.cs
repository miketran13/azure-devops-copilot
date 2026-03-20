using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for project-level metadata queries.
/// </summary>
public sealed class ProjectTools
{
    private readonly AzureDevOpsService _devOps;

    public ProjectTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("List all available work item types for a project (Bug, User Story, Task, Feature, Epic, etc.).")]
    public async Task<string> GetWorkItemTypes(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var types = await _devOps.GetWorkItemTypesAsync(project);
            return JsonSerializer.Serialize(types);
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving work item types for '{project}': {ex.GetType().Name}: {ex.Message}";
        }
    }
}
