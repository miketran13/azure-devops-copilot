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

    [Description("List all Azure DevOps projects accessible in the organization. Use this when you need to ask the user which project to work with.")]
    public async Task<string> ListProjects()
    {
        try
        {
            var projects = await _devOps.GetProjectsAsync();
            return JsonSerializer.Serialize(projects);
        }
        catch (Exception ex)
        {
            return $"ERROR listing projects: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List all iteration paths (sprints) for a project. Returns paths in backslash format (e.g. 'Project\\Sprint 1') which is the correct format for WIQL queries. ALWAYS call this before using iteration paths in WIQL.")]
    public async Task<string> GetIterations(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var iterations = await _devOps.GetIterationsAsync(project);
            return JsonSerializer.Serialize(iterations);
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving iterations for '{project}': {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List all area paths for a project. Use this when you need to ask the user which area path or team area to use.")]
    public async Task<string> GetAreaPaths(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var areas = await _devOps.GetAreaPathsAsync(project);
            return JsonSerializer.Serialize(areas);
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving area paths for '{project}': {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List all team members across all teams in a project. Use this when you need to ask who to assign work to.")]
    public async Task<string> GetTeamMembers(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var members = await _devOps.GetTeamMembersAsync(project);
            return JsonSerializer.Serialize(members);
        }
        catch (Exception ex)
        {
            return $"ERROR retrieving team members for '{project}': {ex.GetType().Name}: {ex.Message}";
        }
    }
}
