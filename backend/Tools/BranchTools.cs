using System.ComponentModel;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for managing Git branches.
/// </summary>
public sealed class BranchTools
{
    private readonly AzureDevOpsService _devOps;

    public BranchTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("Create a new Git branch in an Azure DevOps repository. " +
        "Optionally specify a source branch (defaults to 'main').")]
    public async Task<string> CreateBranch(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("New branch name (e.g. 'feature/my-feature')")] string branchName,
        [Description("Source branch to create from (default: main)")] string sourceRef = "main")
    {
        try
        {
            var newRef = await _devOps.CreateBranchAsync(project, repositoryId, branchName, sourceRef);
            return $"Successfully created branch '{branchName}' (ref: {newRef}) from '{sourceRef}'.";
        }
        catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("403") || ex.Message.Contains("authorization", StringComparison.OrdinalIgnoreCase))
        {
            return $"ERROR creating branch '{branchName}': Authorization failed. " +
                   $"The current token does not have permission to create branches in repository '{repositoryId}'. " +
                   $"To fix this:\n" +
                   $"  1. If using a PAT (local dev): ensure it has 'Code (Read & Write)' scope\n" +
                   $"  2. If using the extension OAuth token: the extension declares 'vso.code_write' scope — " +
                   $"you may need to uninstall and reinstall the extension to re-authorize with the new scope\n" +
                   $"  3. Verify the user has 'Contribute' permission on the repository in Azure DevOps\n" +
                   $"Original error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"ERROR creating branch '{branchName}': {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List branches in a repository. Optionally filter by name prefix.")]
    public async Task<string> ListBranches(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("Optional branch name filter prefix")] string? filter = null)
    {
        try
        {
            var branches = await _devOps.GetBranchesAsync(project, repositoryId, filter);
            if (branches.Count == 0)
                return "No branches found.";
            return $"Branches ({branches.Count}):\n" + string.Join("\n", branches.Select(b => $"  - {b}"));
        }
        catch (Exception ex)
        {
            return $"ERROR listing branches: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
