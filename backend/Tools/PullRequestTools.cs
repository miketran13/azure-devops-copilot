using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for managing Azure DevOps pull requests.
/// </summary>
public sealed class PullRequestTools
{
    private readonly AzureDevOpsService _devOps;

    public PullRequestTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("Create a new pull request in an Azure DevOps Git repository. " +
        "Returns the created PR with its ID and URL.")]
    public async Task<string> CreatePullRequest(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("Source branch name (e.g. 'feature/my-feature')")] string sourceBranch,
        [Description("Target branch name (e.g. 'main')")] string targetBranch,
        [Description("PR title")] string title,
        [Description("PR description (optional, supports markdown)")] string? description = null,
        [Description("Comma-separated work item IDs to link (optional)")] string? workItemIds = null)
    {
        try
        {
            int[]? wiIds = null;
            if (!string.IsNullOrEmpty(workItemIds))
            {
                wiIds = workItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : -1)
                    .Where(id => id > 0)
                    .ToArray();
            }

            var pr = await _devOps.CreatePullRequestAsync(project, repositoryId, sourceBranch, targetBranch, title, description, wiIds);
            return $"Successfully created PR #{pr.Id}: \"{pr.Title}\" ({pr.SourceBranch} → {pr.TargetBranch})\n" +
                   JsonSerializer.Serialize(pr, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR creating pull request: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Update an existing pull request — change title, description, or status.")]
    public async Task<string> UpdatePullRequest(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("Pull request ID")] int pullRequestId,
        [Description("New title (optional)")] string? title = null,
        [Description("New description (optional)")] string? description = null,
        [Description("New status: active, abandoned, completed (optional)")] string? status = null)
    {
        try
        {
            var pr = await _devOps.UpdatePullRequestAsync(project, repositoryId, pullRequestId, title, description, status);
            return $"Successfully updated PR #{pr.Id}: \"{pr.Title}\"\n" +
                   JsonSerializer.Serialize(pr, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR updating PR {pullRequestId}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get details of a specific pull request by ID.")]
    public async Task<string> GetPullRequest(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("Pull request ID")] int pullRequestId)
    {
        try
        {
            var pr = await _devOps.GetPullRequestAsync(project, repositoryId, pullRequestId);
            if (pr is null) return $"Pull request {pullRequestId} not found.";
            return JsonSerializer.Serialize(pr, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR getting PR {pullRequestId}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Search pull requests in a repository. Optionally filter by status (active, completed, abandoned, all).")]
    public async Task<string> SearchPullRequests(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("Status filter: active, completed, abandoned, all (default: active)")] string? status = null,
        [Description("Maximum results to return (default 20)")] int top = 20)
    {
        try
        {
            var prs = await _devOps.SearchPullRequestsAsync(project, repositoryId, status, top);
            if (prs.Count == 0) return "No pull requests found matching the criteria.";
            return JsonSerializer.Serialize(prs, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR searching PRs: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Complete (merge) a pull request. Supports merge strategies: squash, noFastForward, rebase, rebaseMerge.")]
    public async Task<string> CompletePullRequest(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("Pull request ID")] int pullRequestId,
        [Description("Merge strategy: squash, noFastForward, rebase, rebaseMerge (default: squash)")] string mergeStrategy = "squash",
        [Description("Whether to delete the source branch after merge (default: true)")] bool deleteSourceBranch = true)
    {
        try
        {
            var pr = await _devOps.CompletePullRequestAsync(project, repositoryId, pullRequestId, mergeStrategy, deleteSourceBranch);
            return $"Successfully completed PR #{pr.Id}: \"{pr.Title}\" with {mergeStrategy} merge.\n" +
                   JsonSerializer.Serialize(pr, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR completing PR {pullRequestId}: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
