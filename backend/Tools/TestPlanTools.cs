using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for Azure DevOps Test Plan operations.
/// </summary>
public sealed class TestPlanTools
{
    private readonly AzureDevOpsService _devOps;

    public TestPlanTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("List all test plans in a project.")]
    public async Task<string> GetTestPlans(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var plans = await _devOps.GetTestPlansAsync(project);
            if (plans.Count == 0)
                return $"No test plans found in project '{project}'.";
            return JsonSerializer.Serialize(plans, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing test plans: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get test suites within a test plan.")]
    public async Task<string> GetTestSuites(
        [Description("Azure DevOps project name")] string project,
        [Description("Test plan ID")] int planId)
    {
        try
        {
            var suites = await _devOps.GetTestSuitesAsync(project, planId);
            if (suites.Count == 0)
                return $"No test suites found in plan {planId}.";
            return JsonSerializer.Serialize(suites, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing test suites: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get test cases in a test suite within a test plan.")]
    public async Task<string> GetTestCases(
        [Description("Azure DevOps project name")] string project,
        [Description("Test plan ID")] int planId,
        [Description("Test suite ID")] int suiteId)
    {
        try
        {
            var cases = await _devOps.GetTestCasesAsync(project, planId, suiteId);
            if (cases.Count == 0)
                return $"No test cases found in suite {suiteId}.";
            return JsonSerializer.Serialize(cases, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing test cases: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List test runs in a project. Optionally filter by build ID.")]
    public async Task<string> GetTestRuns(
        [Description("Azure DevOps project name")] string project,
        [Description("Filter by build ID (optional)")] int? buildId = null,
        [Description("Maximum number of results (default 25)")] int top = 25)
    {
        try
        {
            var runs = await _devOps.GetTestRunsAsync(project, buildId, top);
            if (runs.Count == 0)
                return "No test runs found.";
            return JsonSerializer.Serialize(runs, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing test runs: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get test results from a specific test run. Shows passed, failed, and skipped tests with error details.")]
    public async Task<string> GetTestResults(
        [Description("Azure DevOps project name")] string project,
        [Description("Test run ID")] int runId)
    {
        try
        {
            var results = await _devOps.GetTestResultsAsync(project, runId);
            if (results.Count == 0)
                return $"No test results found for run {runId}.";
            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting test results: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get code coverage data for a build.")]
    public async Task<string> GetCodeCoverage(
        [Description("Azure DevOps project name")] string project,
        [Description("Build ID")] int buildId)
    {
        try
        {
            var coverage = await _devOps.GetCodeCoverageAsync(project, buildId);
            if (string.IsNullOrEmpty(coverage))
                return $"No code coverage data found for build {buildId}.";
            return coverage;
        }
        catch (Exception ex)
        {
            return $"ERROR getting code coverage: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
