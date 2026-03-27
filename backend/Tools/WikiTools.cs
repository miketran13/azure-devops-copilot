using System.ComponentModel;
using System.Text.Json;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

/// <summary>
/// AI-callable tool functions for Azure DevOps Wiki operations.
/// </summary>
public sealed class WikiTools
{
    private readonly AzureDevOpsService _devOps;

    public WikiTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("List all wikis in an Azure DevOps project. Returns project wikis and code wikis.")]
    public async Task<string> GetWikis(
        [Description("Azure DevOps project name")] string project)
    {
        try
        {
            var wikis = await _devOps.GetWikisAsync(project);
            if (wikis.Count == 0)
                return $"No wikis found in project '{project}'.";
            return JsonSerializer.Serialize(wikis, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"ERROR listing wikis: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Get the content of a wiki page. Returns the page content in Markdown format along with the version (ETag) needed for updates.")]
    public async Task<string> GetWikiPage(
        [Description("Azure DevOps project name")] string project,
        [Description("Wiki ID or name")] string wikiId,
        [Description("Page path (e.g. '/Architecture' or '/Sprint Reviews/Sprint 24')")] string pagePath)
    {
        try
        {
            var (content, version) = await _devOps.GetWikiPageAsync(project, wikiId, pagePath);
            if (content is null)
                return $"Wiki page not found: {pagePath}";
            return $"Page: {pagePath}\nVersion: {version}\n\n{content}";
        }
        catch (Exception ex)
        {
            return $"ERROR reading wiki page: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Create a new wiki page with Markdown content. " +
        "Azure DevOps Wiki supports [[_TOC_]], [[PageName]] links, and Mermaid diagrams.")]
    public async Task<string> CreateWikiPage(
        [Description("Azure DevOps project name")] string project,
        [Description("Wiki ID or name")] string wikiId,
        [Description("Page path (e.g. '/Architecture' or '/Sprint Reviews/Sprint 24')")] string pagePath,
        [Description("Page content in Markdown format")] string content)
    {
        try
        {
            var result = await _devOps.CreateWikiPageAsync(project, wikiId, pagePath, content);
            return $"Wiki page created: {pagePath}\nVersion: {result}";
        }
        catch (Exception ex)
        {
            return $"ERROR creating wiki page: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Update an existing wiki page. You MUST provide the version (ETag) from GetWikiPage to avoid conflicts. " +
        "Always GET the page first to obtain the current version.")]
    public async Task<string> UpdateWikiPage(
        [Description("Azure DevOps project name")] string project,
        [Description("Wiki ID or name")] string wikiId,
        [Description("Page path")] string pagePath,
        [Description("New page content in Markdown format")] string content,
        [Description("Version ETag from GetWikiPage (required for optimistic concurrency)")] string version)
    {
        try
        {
            var newVersion = await _devOps.UpdateWikiPageAsync(project, wikiId, pagePath, content, version);
            return $"Wiki page updated: {pagePath}\nNew version: {newVersion}";
        }
        catch (Exception ex)
        {
            return $"ERROR updating wiki page: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("Delete a wiki page.")]
    public async Task<string> DeleteWikiPage(
        [Description("Azure DevOps project name")] string project,
        [Description("Wiki ID or name")] string wikiId,
        [Description("Page path to delete")] string pagePath)
    {
        try
        {
            await _devOps.DeleteWikiPageAsync(project, wikiId, pagePath);
            return $"Wiki page deleted: {pagePath}";
        }
        catch (Exception ex)
        {
            return $"ERROR deleting wiki page: {ex.GetType().Name}: {ex.Message}";
        }
    }
}
