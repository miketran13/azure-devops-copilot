using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for pipeline, build, release, and variable group operations.
/// </summary>
public sealed class PipelineTools
{
    private readonly AzureDevOpsService _devOps;

    public PipelineTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("List all pipelines (build definitions) in an Azure DevOps project.")]
    public async Task<string> GetPipelines(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var pipelines = await _devOps.GetPipelinesAsync(project);
            if (pipelines.Count == 0)
                return $"No pipelines found in project '{project}'.";
            return JsonSerializer.Serialize(pipelines, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing pipelines: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get details of a specific pipeline by ID.")]
    public async Task<string> GetPipeline(
        [Description("Azure DevOps project name")] string project,
        [Description("Pipeline (build definition) ID")] int pipelineId)
    {
        try
        {
            var pipeline = await _devOps.GetPipelineAsync(project, pipelineId);
            if (pipeline is null)
                return $"Pipeline {pipelineId} not found in project '{project}'.";
            return JsonSerializer.Serialize(pipeline, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting pipeline: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Trigger a pipeline run. Optionally specify the branch and variables. " +
        "Returns the build run details including the run ID for monitoring.")]
    public async Task<string> RunPipeline(
        [Description("Azure DevOps project name")] string project,
        [Description("Pipeline (build definition) ID")] int pipelineId,
        [Description("Branch to build (optional, defaults to pipeline default)")] string? branch = null,
        [Description("Pipeline variables as JSON object, e.g. {\"key\": \"value\"} (optional)")] string? variablesJson = null)
    {
        try
        {
            Dictionary<string, string>? variables = null;
            if (!string.IsNullOrWhiteSpace(variablesJson))
            {
                variables = JsonSerializer.Deserialize<Dictionary<string, string>>(variablesJson);
            }

            var run = await _devOps.RunPipelineAsync(project, pipelineId, branch, variables);
            return JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR running pipeline: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List recent pipeline runs (builds) for a project. " +
        "Optionally filter by pipeline ID, status, or branch.")]
    public async Task<string> GetPipelineRuns(
        [Description("Azure DevOps project name")] string project,
        [Description("Pipeline (build definition) ID to filter (optional)")] int? pipelineId = null,
        [Description("Filter by status: completed, inProgress, cancelling, notStarted (optional)")] string? status = null,
        [Description("Filter by branch name (optional)")] string? branch = null,
        [Description("Maximum number of results (default 20)")] int top = 20)
    {
        try
        {
            var runs = await _devOps.GetPipelineRunsAsync(project, pipelineId, status, branch, top);
            if (runs.Count == 0)
                return "No pipeline runs found matching the criteria.";
            return JsonSerializer.Serialize(runs, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing pipeline runs: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get build logs for a specific build. Returns the log content to analyze build failures.")]
    public async Task<string> GetBuildLogs(
        [Description("Azure DevOps project name")] string project,
        [Description("Build ID")] int buildId)
    {
        try
        {
            var logs = await _devOps.GetBuildLogsAsync(project, buildId);
            if (string.IsNullOrEmpty(logs))
                return $"No logs found for build {buildId}.";

            const int maxChars = 50_000;
            if (logs.Length > maxChars)
                return $"Build logs (truncated to {maxChars} chars):\n{logs[..maxChars]}\n\n... (truncated)";

            return $"Build logs for build {buildId}:\n{logs}";
        }
        catch (Exception ex)
        {
            return $"ERROR getting build logs: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get the timeline of a build showing all tasks/steps and their status. " +
        "Useful for identifying which step failed in a build.")]
    public async Task<string> GetBuildTimeline(
        [Description("Azure DevOps project name")] string project,
        [Description("Build ID")] int buildId)
    {
        try
        {
            var timeline = await _devOps.GetBuildTimelineAsync(project, buildId);
            if (timeline.Count == 0)
                return $"No timeline data found for build {buildId}.";
            return JsonSerializer.Serialize(timeline, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting build timeline: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Cancel a running build.")]
    public async Task<string> CancelBuild(
        [Description("Azure DevOps project name")] string project,
        [Description("Build ID to cancel")] int buildId)
    {
        try
        {
            await _devOps.CancelBuildAsync(project, buildId);
            return $"Build {buildId} has been cancelled.";
        }
        catch (Exception ex)
        {
            return $"ERROR cancelling build: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get build artifacts for a completed build.")]
    public async Task<string> GetBuildArtifacts(
        [Description("Azure DevOps project name")] string project,
        [Description("Build ID")] int buildId)
    {
        try
        {
            var artifacts = await _devOps.GetBuildArtifactsAsync(project, buildId);
            if (artifacts.Count == 0)
                return $"No artifacts found for build {buildId}.";
            return JsonSerializer.Serialize(artifacts, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting build artifacts: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List variable groups in a project. Variable groups contain shared variables used by pipelines.")]
    public async Task<string> GetVariableGroups(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var groups = await _devOps.GetVariableGroupsAsync(project);
            if (groups.Count == 0)
                return $"No variable groups found in project '{project}'.";
            return JsonSerializer.Serialize(groups, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing variable groups: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Add or update a variable in a variable group. " +
        "For secret variables, set isSecret=true. Secret values are write-only and cannot be read back.")]
    public async Task<string> AddPipelineVariable(
        [Description("Azure DevOps project name")] string project,
        [Description("Variable group ID")] int groupId,
        [Description("Variable name")] string variableName,
        [Description("Variable value")] string variableValue,
        [Description("Whether this is a secret variable (default false)")] bool isSecret = false)
    {
        try
        {
            await _devOps.AddVariableToGroupAsync(project, groupId, variableName, variableValue, isSecret);
            var secretNote = isSecret ? " (secret — value is masked)" : $" = \"{variableValue}\"";
            return $"Variable '{variableName}'{secretNote} added to variable group {groupId}.";
        }
        catch (Exception ex)
        {
            return $"ERROR adding variable: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List agent pools available in the organization.")]
    public async Task<string> GetAgentPools()
    {
        try
        {
            var pools = await _devOps.GetAgentPoolsAsync();
            if (pools.Count == 0)
                return "No agent pools found.";
            return JsonSerializer.Serialize(pools, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing agent pools: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get failed test results from a specific build run. " +
        "Shows which tests failed, their error messages, and stack traces.")]
    public async Task<string> GetFailedTestResults(
        [Description("Azure DevOps project name")] string project,
        [Description("Build ID")] int buildId)
    {
        try
        {
            var results = await _devOps.GetTestResultsForBuildAsync(project, buildId, outcomeFilter: "Failed");
            if (results.Count == 0)
                return $"No failed tests found for build {buildId}. All tests passed! ✅";
            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting test results: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
