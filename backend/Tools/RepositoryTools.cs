using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for browsing repositories and reading code.
/// </summary>
public sealed class RepositoryTools
{
    private readonly AzureDevOpsService _devOps;

    public RepositoryTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("List all Git repositories in an Azure DevOps project.")]
    public async Task<string> ListRepositories(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var repos = await _devOps.GetRepositoriesAsync(project);
            if (repos.Count == 0)
                return $"No repositories found in project '{project}'.";
            return JsonSerializer.Serialize(repos, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing repositories: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Read the contents of a specific file from a repository. " +
        "Returns the file text content. Binary files are not supported.")]
    public async Task<string> GetFileContent(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("File path in the repository (e.g. '/src/Program.cs')")] string path,
        [Description("Branch name (optional, defaults to default branch)")] string? branch = null)
    {
        try
        {
            var content = await _devOps.GetFileContentAsync(project, repositoryId, path, branch);
            if (content is null)
                return $"File not found: {path}";

            // Safety: limit response size
            const int maxChars = 100_000;
            if (content.Length > maxChars)
                return $"File '{path}' is too large ({content.Length} chars). Showing first {maxChars} characters:\n\n{content[..maxChars]}\n\n... (truncated)";

            return $"File: {path}\n```\n{content}\n```";
        }
        catch (Exception ex)
        {
            return $"ERROR reading file '{path}': {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Browse the directory tree of a repository. " +
        "Returns a list of file/folder paths. Optionally scope to a specific folder.")]
    public async Task<string> GetDirectoryTree(
        [Description("Azure DevOps project name")] string project,
        [Description("Repository name or ID")] string repositoryId,
        [Description("Folder path to scope (optional, e.g. '/src')")] string? scopePath = null,
        [Description("Branch name (optional)")] string? branch = null)
    {
        try
        {
            var paths = await _devOps.GetDirectoryTreeAsync(project, repositoryId, scopePath, branch);
            if (paths.Count == 0)
                return "No items found in the directory.";
            return $"Directory tree ({paths.Count} items):\n" + string.Join("\n", paths.Select(p => $"  {p}"));
        }
        catch (Exception ex)
        {
            return $"ERROR browsing directory: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
