using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevOpsCopilot.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Services;

/// <summary>
/// Calls the Azure DevOps REST API on behalf of the signed-in user.
/// Uses their OAuth Bearer token (forwarded from the extension SDK) so all
/// operations are scoped to their actual ADO permissions.
///
/// For local development, set AzureDevOps:Pat in local.settings.json to use a
/// Personal Access Token instead — this is only needed because the extension SDK
/// token cannot easily be tested outside of a real ADO context.
/// </summary>
public sealed class AzureDevOpsService : IDisposable
{
    private const string ApiVersion = "7.1";

    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _defaultOrgUrl;
    private readonly string? _configuredPat;

    private static readonly TimeSpan MetadataCacheTtl = TimeSpan.FromMinutes(5);

    private HttpClient? _http;
    private string? _orgUrl;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AzureDevOpsService(IConfiguration configuration, ILogger<AzureDevOpsService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _defaultOrgUrl = configuration["AzureDevOps:DefaultOrganizationUrl"]
            ?? throw new InvalidOperationException("AzureDevOps:DefaultOrganizationUrl is not configured.");
        _configuredPat = configuration["AzureDevOps:Pat"];
    }

    /// <summary>
    /// Initialize with the user's OAuth access token forwarded from the Azure DevOps
    /// extension SDK (getAccessToken()). This is sent as Authorization: Bearer so ADO
    /// enforces the user's own permissions on every request.
    ///
    /// If AzureDevOps:Pat is configured (local dev only), that takes priority.
    /// </summary>
    public void Initialize(string accessToken, string? organizationUrl = null)
    {
        _orgUrl = (organizationUrl ?? _defaultOrgUrl).TrimEnd('/');

        // PAT takes priority for local dev (set AzureDevOps:Pat in local.settings.json)
        // Ignore common placeholder values so we fall through to the OAuth token path
        var hasPat = !string.IsNullOrWhiteSpace(_configuredPat)
                  && !_configuredPat.StartsWith("YOUR-", StringComparison.OrdinalIgnoreCase)
                  && !_configuredPat.StartsWith("PASTE", StringComparison.OrdinalIgnoreCase)
                  && !_configuredPat.Contains("_HERE", StringComparison.OrdinalIgnoreCase)
                  && !_configuredPat.Contains("REPLACE", StringComparison.OrdinalIgnoreCase);
        var token = hasPat ? _configuredPat! : accessToken;
        var scheme = hasPat ? "Basic" : "Bearer";

        // PAT must be base64-encoded as ":pat"
        var headerValue = scheme == "Basic"
            ? Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}"))
            : token;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, headerValue);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogInformation("AzureDevOpsService initialized ({Scheme}) for {OrgUrl}", scheme, _orgUrl);
    }

    private HttpClient Http => _http
        ?? throw new InvalidOperationException("AzureDevOpsService not initialized. Call Initialize() first.");

    private string OrgUrl => _orgUrl
        ?? throw new InvalidOperationException("AzureDevOpsService not initialized. Call Initialize() first.");

    // ─── Search / Query ────────────────────────────────────────────────

    public async Task<List<WorkItemSummary>> SearchWorkItemsAsync(
        string wiqlWhereClause, string project, int top = 50)
    {
        _logger.LogInformation("Searching work items in {Project}: {Where}", project, wiqlWhereClause);

        var query = $@"SELECT [System.Id] FROM WorkItems WHERE {wiqlWhereClause} ORDER BY [System.ChangedDate] DESC";
        var body = JsonSerializer.Serialize(new { query });
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/wiql?$top={top}&api-version={ApiVersion}";

        var response = await Http.PostAsync(url,
            new StringContent(body, Encoding.UTF8, "application/json"));
        await EnsureSuccessAsync(response, $"WIQL query in project '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var ids = json?["workItems"]?.AsArray()
            .Select(wi => wi?["id"]?.GetValue<int>())
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray() ?? [];

        if (ids.Length == 0) return [];
        return await GetWorkItemsByIdsAsync(ids);
    }

    public async Task<WorkItemSummary?> GetWorkItemAsync(int id)
    {
        _logger.LogInformation("Getting work item {Id}", id);

        var url = $"{OrgUrl}/_apis/wit/workitems/{id}?$expand=fields&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, $"get work item {id}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json is null ? null : MapToSummary(json);
    }

    public async Task<List<WorkItemSummary>> GetWorkItemsByIdsAsync(int[] ids)
    {
        if (ids.Length == 0) return [];

        var fields = "System.Id,System.Title,System.State,System.WorkItemType," +
                     "System.AssignedTo,System.AreaPath,System.IterationPath," +
                     "System.Description,System.Tags,Microsoft.VSTS.Common.Priority," +
                     "Microsoft.VSTS.Scheduling.StoryPoints,Microsoft.VSTS.Common.ValueArea," +
                     "System.CreatedDate,System.ChangedDate";

        var allItems = new List<WorkItemSummary>();
        foreach (var batch in ids.Chunk(200))
        {
            var idList = string.Join(",", batch);
            var url = $"{OrgUrl}/_apis/wit/workitems?ids={idList}&fields={fields}&api-version={ApiVersion}";
            var response = await Http.GetAsync(url);
            await EnsureSuccessAsync(response, $"get work items [{idList}]");

            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            var items = json?["value"]?.AsArray()
                .Where(n => n is not null)
                .Select(n => MapToSummary(n!))
                .ToList() ?? [];
            allItems.AddRange(items);
        }

        return allItems;
    }

    // ─── Create ────────────────────────────────────────────────────────

    public async Task<WorkItemSummary> CreateWorkItemAsync(
        string project, string workItemType, string title,
        string? description = null, string? assignedTo = null,
        string? areaPath = null, string? iterationPath = null,
        string? tags = null, int? priority = null,
        double? storyPoints = null, string? valueArea = null)
    {
        _logger.LogInformation("Creating {Type} in {Project}: {Title}", workItemType, project, title);

        var ops = new List<object>
        {
            Op("add", "/fields/System.Title", title)
        };
        if (description is not null) ops.Add(Op("add", "/fields/System.Description", description));
        if (assignedTo is not null) ops.Add(Op("add", "/fields/System.AssignedTo", assignedTo));
        if (areaPath is not null) ops.Add(Op("add", "/fields/System.AreaPath", areaPath));
        if (iterationPath is not null) ops.Add(Op("add", "/fields/System.IterationPath", iterationPath));
        if (tags is not null) ops.Add(Op("add", "/fields/System.Tags", tags));
        if (priority.HasValue) ops.Add(Op("add", "/fields/Microsoft.VSTS.Common.Priority", priority.Value));
        if (storyPoints.HasValue) ops.Add(Op("add", "/fields/Microsoft.VSTS.Scheduling.StoryPoints", storyPoints.Value));
        if (valueArea is not null) ops.Add(Op("add", "/fields/Microsoft.VSTS.Common.ValueArea", valueArea));

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitems/" +
                  $"${Uri.EscapeDataString(workItemType)}?api-version={ApiVersion}";

        var content = new StringContent(JsonSerializer.Serialize(ops), Encoding.UTF8, "application/json-patch+json");
        var response = await Http.PostAsync(url, content);
        await EnsureSuccessAsync(response, $"create {workItemType} in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToSummary(json);
    }

    // ─── Update ────────────────────────────────────────────────────────

    public async Task<WorkItemSummary> UpdateWorkItemAsync(
        int workItemId, Dictionary<string, string> fieldUpdates)
    {
        _logger.LogInformation("Updating work item {Id} with {Count} field(s)", workItemId, fieldUpdates.Count);

        var ops = fieldUpdates.Select(kv => Op("add", $"/fields/{kv.Key}", kv.Value)).ToList();
        var url = $"{OrgUrl}/_apis/wit/workitems/{workItemId}?api-version={ApiVersion}";

        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(JsonSerializer.Serialize(ops), Encoding.UTF8, "application/json-patch+json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"update work item {workItemId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToSummary(json);
    }

    // ─── Project / Metadata ────────────────────────────────────────────

    public async Task<List<string>> GetWorkItemTypesAsync(string project)
    {
        var cacheKey = $"wit-types:{OrgUrl}:{project}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitemtypes?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get work item types for '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var result = json?["value"]?.AsArray()
            .Select(t => t?["name"]?.GetValue<string>())
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList() ?? [];

        _cache.Set(cacheKey, result, MetadataCacheTtl);
        return result;
    }

    public async Task<List<string>> GetProjectsAsync()
    {
        var cacheKey = $"projects:{OrgUrl}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;

        var url = $"{OrgUrl}/_apis/projects?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, "list projects");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var result = json?["value"]?.AsArray()
            .Select(p => p?["name"]?.GetValue<string>())
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList() ?? [];

        _cache.Set(cacheKey, result, MetadataCacheTtl);
        return result;
    }

    public async Task<List<string>> GetIterationsAsync(string project)
    {
        var cacheKey = $"iterations:{OrgUrl}:{project}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/classificationnodes/iterations?$depth=10&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get iterations for '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var results = new List<string>();
        CollectClassificationPaths(json, results);

        _cache.Set(cacheKey, results, MetadataCacheTtl);
        return results;
    }

    public async Task<List<string>> GetAreaPathsAsync(string project)
    {
        var cacheKey = $"areas:{OrgUrl}:{project}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/classificationnodes/areas?$depth=10&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get area paths for '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var results = new List<string>();
        CollectClassificationPaths(json, results);

        _cache.Set(cacheKey, results, MetadataCacheTtl);
        return results;
    }

    public async Task<List<string>> GetTeamMembersAsync(string project)
    {
        var cacheKey = $"team-members:{OrgUrl}:{project}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            return cached;
        var teamsUrl = $"{OrgUrl}/_apis/projects/{Uri.EscapeDataString(project)}/teams?api-version={ApiVersion}";
        var teamsResponse = await Http.GetAsync(teamsUrl);
        await EnsureSuccessAsync(teamsResponse, $"get teams for '{project}'");

        var teamsJson = JsonNode.Parse(await teamsResponse.Content.ReadAsStringAsync());
        var teams = teamsJson?["value"]?.AsArray();
        if (teams is null || teams.Count == 0) return [];

        var members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in teams)
        {
            var teamId = team?["id"]?.GetValue<string>();
            if (teamId is null) continue;
            try
            {
                var membersUrl = $"{OrgUrl}/_apis/projects/{Uri.EscapeDataString(project)}/teams/{teamId}/members?api-version={ApiVersion}";
                var membersResponse = await Http.GetAsync(membersUrl);
                if (!membersResponse.IsSuccessStatusCode) continue;

                var membersJson = JsonNode.Parse(await membersResponse.Content.ReadAsStringAsync());
                var memberArray = membersJson?["value"]?.AsArray();
                if (memberArray is null) continue;

                foreach (var m in memberArray)
                {
                    var name = m?["identity"]?["displayName"]?.GetValue<string>();
                    if (!string.IsNullOrEmpty(name))
                        members.Add(name);
                }
            }
            catch { /* skip teams with access issues */ }
        }

        var result = members.OrderBy(n => n).ToList();
        _cache.Set(cacheKey, result, MetadataCacheTtl);
        return result;
    }

    private static void CollectClassificationPaths(JsonNode? node, List<string> paths)
    {
        if (node is null) return;
        var path = node["path"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(path))
            paths.Add(path.TrimStart('\\'));

        var children = node["children"]?.AsArray();
        if (children is not null)
        {
            foreach (var child in children)
                CollectClassificationPaths(child, paths);
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private static object Op(string op, string path, object value) =>
        new { op, path, value };

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        var statusCode = (int)response.StatusCode;

        // Log raw details at Debug level only — never expose in thrown exceptions
        _logger.LogDebug("ADO API error response for {Operation}: {StatusCode} {Body}",
            operation, statusCode, body);

        // Provide more helpful guidance for auth/permission errors
        if (statusCode == 401 || statusCode == 403)
        {
            var hint = operation.Contains("branch") || operation.Contains("PR") || operation.Contains("pull request") || operation.Contains("repositor") || operation.Contains("file")
                ? " Hint: Git/Code operations require the 'vso.code_write' scope. " +
                  "If using a PAT, ensure it has 'Code (Read & Write)' permission. " +
                  "If using the extension OAuth token, the user may need to re-authorize the extension " +
                  "after scope changes — uninstall and reinstall the extension, or ask the admin to approve the new scope."
                : " Hint: Ensure the token has sufficient permissions for this operation.";
            throw new InvalidOperationException(
                $"Azure DevOps authorization error ({statusCode}) during {operation}.{hint}");
        }

        throw new InvalidOperationException(
            $"Azure DevOps API error ({statusCode}) during {operation}. Check logs for details.");
    }

    private static WorkItemSummary MapToSummary(JsonNode wi)
    {
        var fields = wi["fields"];
        string? F(string name) => fields?[name]?.GetValue<string>();
        int? FInt(string name) => fields?[name]?.GetValue<int?>() ?? null;
        double? FDbl(string name) { try { return fields?[name]?.GetValue<double?>(); } catch { return null; } }
        DateTime? FDate(string name) => fields?[name] is { } n
            ? n.GetValue<DateTime?>() : null;

        // AssignedTo is a nested object with displayName
        var assignedTo = fields?["System.AssignedTo"]?["displayName"]?.GetValue<string>()
                      ?? fields?["System.AssignedTo"]?.GetValue<string>();

        return new WorkItemSummary
        {
            Id = wi["id"]?.GetValue<int>() ?? 0,
            Title = F("System.Title") ?? string.Empty,
            WorkItemType = F("System.WorkItemType") ?? string.Empty,
            State = F("System.State") ?? string.Empty,
            AssignedTo = assignedTo,
            AreaPath = F("System.AreaPath"),
            IterationPath = F("System.IterationPath"),
            Description = F("System.Description"),
            Tags = F("System.Tags"),
            Priority = FInt("Microsoft.VSTS.Common.Priority"),
            StoryPoints = FDbl("Microsoft.VSTS.Scheduling.StoryPoints"),
            ValueArea = F("Microsoft.VSTS.Common.ValueArea"),
            CreatedDate = FDate("System.CreatedDate"),
            ChangedDate = FDate("System.ChangedDate"),
            Url = wi["url"]?.GetValue<string>(),
        };
    }

    // ─── Pull Request Operations ───────────────────────────────────────

    public async Task<List<RepositorySummary>> GetRepositoriesAsync(string project)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get repositories for '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(r => new RepositorySummary
            {
                Id = r?["id"]?.GetValue<string>() ?? string.Empty,
                Name = r?["name"]?.GetValue<string>() ?? string.Empty,
                DefaultBranch = r?["defaultBranch"]?.GetValue<string>(),
                Url = r?["url"]?.GetValue<string>(),
                WebUrl = r?["webUrl"]?.GetValue<string>(),
                Size = r?["size"]?.GetValue<long?>(),
            })
            .ToList() ?? [];
    }

    public async Task<PullRequestSummary> CreatePullRequestAsync(
        string project, string repositoryId,
        string sourceBranch, string targetBranch, string title,
        string? description = null, int[]? workItemIds = null)
    {
        _logger.LogInformation("Creating PR in {Project}/{Repo}: {Title}", project, repositoryId, title);

        // Ensure branch refs are full paths
        if (!sourceBranch.StartsWith("refs/")) sourceBranch = $"refs/heads/{sourceBranch}";
        if (!targetBranch.StartsWith("refs/")) targetBranch = $"refs/heads/{targetBranch}";

        var body = new Dictionary<string, object>
        {
            ["sourceRefName"] = sourceBranch,
            ["targetRefName"] = targetBranch,
            ["title"] = title,
        };
        if (description is not null) body["description"] = description;
        if (workItemIds is { Length: > 0 })
        {
            body["workItemRefs"] = workItemIds.Select(id => new { id = id.ToString() }).ToArray();
        }

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/pullrequests?api-version={ApiVersion}";
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await Http.PostAsync(url, content);
        await EnsureSuccessAsync(response, $"create pull request in '{project}/{repositoryId}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToPullRequestSummary(json);
    }

    public async Task<PullRequestSummary> UpdatePullRequestAsync(
        string project, string repositoryId, int pullRequestId,
        string? title = null, string? description = null, string? status = null)
    {
        _logger.LogInformation("Updating PR {PrId} in {Project}/{Repo}", pullRequestId, project, repositoryId);

        var body = new Dictionary<string, object>();
        if (title is not null) body["title"] = title;
        if (description is not null) body["description"] = description;
        if (status is not null) body["status"] = status;

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/pullrequests/{pullRequestId}?api-version={ApiVersion}";
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"update PR {pullRequestId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToPullRequestSummary(json);
    }

    public async Task<PullRequestSummary?> GetPullRequestAsync(
        string project, string repositoryId, int pullRequestId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/pullrequests/{pullRequestId}?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, $"get PR {pullRequestId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToPullRequestSummary(json);
    }

    public async Task<List<PullRequestSummary>> SearchPullRequestsAsync(
        string project, string repositoryId, string? status = null, int top = 20)
    {
        var statusFilter = status ?? "active";
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/pullrequests?searchCriteria.status={statusFilter}&$top={top}&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"search PRs in '{project}/{repositoryId}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Where(n => n is not null)
            .Select(n => MapToPullRequestSummary(n!))
            .ToList() ?? [];
    }

    public async Task<PullRequestSummary> CompletePullRequestAsync(
        string project, string repositoryId, int pullRequestId,
        string mergeStrategy = "squash", bool deleteSourceBranch = true)
    {
        _logger.LogInformation("Completing PR {PrId} with {Strategy}", pullRequestId, mergeStrategy);

        // First get the PR to obtain the last merge source commit
        var pr = await GetPullRequestAsync(project, repositoryId, pullRequestId)
            ?? throw new InvalidOperationException($"PR {pullRequestId} not found");

        var body = new
        {
            status = "completed",
            lastMergeSourceCommit = new { commitId = (string?)null }, // API requires this
            completionOptions = new
            {
                mergeStrategy,
                deleteSourceBranch,
                mergeCommitMessage = $"Merged PR {pullRequestId}: {pr.Title}",
            }
        };

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/pullrequests/{pullRequestId}?api-version={ApiVersion}";
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"complete PR {pullRequestId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToPullRequestSummary(json);
    }

    // ─── Branch Operations ─────────────────────────────────────────────

    public async Task<string> CreateBranchAsync(
        string project, string repositoryId, string branchName, string sourceRef = "main")
    {
        _logger.LogInformation("Creating branch {Branch} from {Source} in {Repo}", branchName, sourceRef, repositoryId);

        // Resolve the source ref to get its object ID
        if (!sourceRef.StartsWith("refs/")) sourceRef = $"refs/heads/{sourceRef}";
        var refsUrl = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/refs?filter=heads/{sourceRef.Replace("refs/heads/", "")}&api-version={ApiVersion}";
        var refsResponse = await Http.GetAsync(refsUrl);
        await EnsureSuccessAsync(refsResponse, $"resolve ref '{sourceRef}'");

        var refsJson = JsonNode.Parse(await refsResponse.Content.ReadAsStringAsync());
        var objectId = refsJson?["value"]?.AsArray().FirstOrDefault()?["objectId"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Could not resolve ref '{sourceRef}' — branch may not exist.");

        // Create the new branch
        var newRef = branchName.StartsWith("refs/") ? branchName : $"refs/heads/{branchName}";
        var body = new[]
        {
            new
            {
                name = newRef,
                newObjectId = objectId,
                oldObjectId = "0000000000000000000000000000000000000000"
            }
        };

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/refs?api-version={ApiVersion}";
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await Http.PostAsync(url, content);
        await EnsureSuccessAsync(response, $"create branch '{branchName}'");

        return newRef;
    }

    public async Task<List<string>> GetBranchesAsync(
        string project, string repositoryId, string? filter = null)
    {
        var filterParam = filter is not null ? $"&filter=heads/{filter}" : "";
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/refs?filter=heads/{filterParam}&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get branches for '{repositoryId}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(r => r?["name"]?.GetValue<string>()?.Replace("refs/heads/", "") ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList() ?? [];
    }

    // ─── Work Item Link Operations ─────────────────────────────────────

    public async Task<WorkItemSummary> AddWorkItemLinkAsync(
        int sourceId, int targetId, string linkType = "System.LinkTypes.Hierarchy-Forward")
    {
        _logger.LogInformation("Linking work item {Source} -> {Target} ({LinkType})", sourceId, targetId, linkType);

        var targetUrl = $"{OrgUrl}/_apis/wit/workitems/{targetId}";
        var ops = new[]
        {
            new
            {
                op = "add",
                path = "/relations/-",
                value = new
                {
                    rel = linkType,
                    url = targetUrl,
                    attributes = new { comment = $"Linked by DevOps Copilot" }
                }
            }
        };

        var url = $"{OrgUrl}/_apis/wit/workitems/{sourceId}?api-version={ApiVersion}";
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(JsonSerializer.Serialize(ops), Encoding.UTF8, "application/json-patch+json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"link {sourceId} -> {targetId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToSummary(json);
    }

    public async Task<List<object>> GetWorkItemLinksAsync(int workItemId)
    {
        var url = $"{OrgUrl}/_apis/wit/workitems/{workItemId}?$expand=relations&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get links for work item {workItemId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var relations = json?["relations"]?.AsArray();
        if (relations is null || relations.Count == 0)
            return [];

        return relations.Select(r => (object)new
        {
            rel = r?["rel"]?.GetValue<string>(),
            url = r?["url"]?.GetValue<string>(),
            attributes = r?["attributes"]
        }).ToList();
    }

    // ─── Attachment Operations ─────────────────────────────────────────

    /// <summary>
    /// Downloads the content of an attachment by its URL. Returns raw bytes.
    /// </summary>
    public async Task<byte[]> DownloadAttachmentAsync(string attachmentUrl)
    {
        _logger.LogInformation("Downloading attachment from {Url}", attachmentUrl);
        var response = await Http.GetAsync(attachmentUrl);
        await EnsureSuccessAsync(response, "download attachment");
        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    /// Uploads a file as an attachment to Azure DevOps and returns the attachment URL.
    /// </summary>
    public async Task<string> UploadAttachmentAsync(string fileName, byte[] fileContent)
    {
        _logger.LogInformation("Uploading attachment '{FileName}' ({Size} bytes)", fileName, fileContent.Length);

        var url = $"{OrgUrl}/_apis/wit/attachments?fileName={Uri.EscapeDataString(fileName)}&api-version={ApiVersion}";
        var content = new ByteArrayContent(fileContent);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = await Http.PostAsync(url, content);
        await EnsureSuccessAsync(response, $"upload attachment '{fileName}'");

        var json = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["url"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Attachment upload succeeded but no URL was returned.");
    }

    /// <summary>
    /// Links a previously uploaded attachment to a work item.
    /// </summary>
    public async Task<WorkItemSummary> AddAttachmentToWorkItemAsync(
        int workItemId, string attachmentUrl, string fileName, string? comment = null)
    {
        _logger.LogInformation("Attaching '{FileName}' to work item #{Id}", fileName, workItemId);

        var ops = new[]
        {
            new
            {
                op = "add",
                path = "/relations/-",
                value = new
                {
                    rel = "AttachedFile",
                    url = attachmentUrl,
                    attributes = new
                    {
                        comment = comment ?? $"Attached by DevOps Copilot: {fileName}",
                        name = fileName,
                    }
                }
            }
        };

        var url = $"{OrgUrl}/_apis/wit/workitems/{workItemId}?api-version={ApiVersion}";
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(ops),
                Encoding.UTF8,
                "application/json-patch+json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"attach file to work item {workItemId}");

        var json = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return MapToSummary(json);
    }

    // ─── Repository / Code Operations ──────────────────────────────────

    public async Task<string?> GetFileContentAsync(
        string project, string repositoryId, string path, string? branch = null)
    {
        var versionParam = branch is not null ? $"&versionDescriptor.version={Uri.EscapeDataString(branch)}&versionDescriptor.versionType=branch" : "";
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/items?path={Uri.EscapeDataString(path)}{versionParam}&api-version={ApiVersion}";

        var response = await Http.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, $"get file '{path}' from '{repositoryId}'");

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<string>> GetDirectoryTreeAsync(
        string project, string repositoryId, string? scopePath = null, string? branch = null)
    {
        var pathParam = scopePath is not null ? $"&scopePath={Uri.EscapeDataString(scopePath)}" : "";
        var versionParam = branch is not null ? $"&versionDescriptor.version={Uri.EscapeDataString(branch)}&versionDescriptor.versionType=branch" : "";
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/git/repositories/{Uri.EscapeDataString(repositoryId)}/items?recursionLevel=full{pathParam}{versionParam}&api-version={ApiVersion}";

        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get directory tree for '{repositoryId}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(i => i?["path"]?.GetValue<string>() ?? "")
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList() ?? [];
    }

    // ─── Pipeline / Build Operations ──────────────────────────────────

    public async Task<List<object>> GetPipelinesAsync(string project)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/pipelines?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list pipelines in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(p => (object)new
            {
                id = p?["id"]?.GetValue<int>(),
                name = p?["name"]?.GetValue<string>(),
                folder = p?["folder"]?.GetValue<string>(),
                url = p?["url"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<object?> GetPipelineAsync(string project, int pipelineId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/pipelines/{pipelineId}?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, $"get pipeline {pipelineId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return new
        {
            id = json?["id"]?.GetValue<int>(),
            name = json?["name"]?.GetValue<string>(),
            folder = json?["folder"]?.GetValue<string>(),
            configuration = json?["configuration"],
            url = json?["url"]?.GetValue<string>(),
        };
    }

    public async Task<object> RunPipelineAsync(
        string project, int pipelineId, string? branch = null, Dictionary<string, string>? variables = null)
    {
        _logger.LogInformation("Running pipeline {PipelineId} in {Project}", pipelineId, project);

        var body = new Dictionary<string, object>();
        if (branch is not null)
        {
            var branchRef = branch.StartsWith("refs/") ? branch : $"refs/heads/{branch}";
            body["resources"] = new { repositories = new { self = new { refName = branchRef } } };
        }
        if (variables is { Count: > 0 })
        {
            body["variables"] = variables.ToDictionary(
                kv => kv.Key,
                kv => (object)new { value = kv.Value });
        }

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/pipelines/{pipelineId}/runs?api-version={ApiVersion}";
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await Http.PostAsync(url, content);
        await EnsureSuccessAsync(response, $"run pipeline {pipelineId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return new
        {
            id = json?["id"]?.GetValue<int>(),
            name = json?["name"]?.GetValue<string>(),
            state = json?["state"]?.GetValue<string>(),
            result = json?["result"]?.GetValue<string>(),
            createdDate = json?["createdDate"]?.GetValue<string>(),
            url = json?["url"]?.GetValue<string>(),
            webUrl = json?["_links"]?["web"]?["href"]?.GetValue<string>(),
        };
    }

    public async Task<List<object>> GetPipelineRunsAsync(
        string project, int? pipelineId = null, string? status = null, string? branch = null, int top = 20)
    {
        // Use the builds API which is richer than the pipelines/runs API
        var queryParts = new List<string> { $"$top={top}", $"api-version={ApiVersion}" };
        if (pipelineId.HasValue) queryParts.Add($"definitions={pipelineId.Value}");
        if (!string.IsNullOrEmpty(status)) queryParts.Add($"statusFilter={status}");
        if (!string.IsNullOrEmpty(branch))
        {
            var branchRef = branch.StartsWith("refs/") ? branch : $"refs/heads/{branch}";
            queryParts.Add($"branchName={Uri.EscapeDataString(branchRef)}");
        }

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/build/builds?{string.Join("&", queryParts)}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list builds in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(b => (object)new
            {
                id = b?["id"]?.GetValue<int>(),
                buildNumber = b?["buildNumber"]?.GetValue<string>(),
                status = b?["status"]?.GetValue<string>(),
                result = b?["result"]?.GetValue<string>(),
                definition = b?["definition"]?["name"]?.GetValue<string>(),
                sourceBranch = b?["sourceBranch"]?.GetValue<string>()?.Replace("refs/heads/", ""),
                requestedBy = b?["requestedBy"]?["displayName"]?.GetValue<string>(),
                startTime = b?["startTime"]?.GetValue<string>(),
                finishTime = b?["finishTime"]?.GetValue<string>(),
                url = b?["_links"]?["web"]?["href"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<string> GetBuildLogsAsync(string project, int buildId)
    {
        _logger.LogInformation("Getting build logs for build {BuildId}", buildId);

        // First get the list of log entries
        var listUrl = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/build/builds/{buildId}/logs?api-version={ApiVersion}";
        var listResponse = await Http.GetAsync(listUrl);
        await EnsureSuccessAsync(listResponse, $"list build logs for build {buildId}");

        var listJson = JsonNode.Parse(await listResponse.Content.ReadAsStringAsync());
        var logEntries = listJson?["value"]?.AsArray();
        if (logEntries is null || logEntries.Count == 0) return string.Empty;

        // Get the last log entry (usually the most relevant for failure analysis)
        var lastLogId = logEntries.Last()?["id"]?.GetValue<int>() ?? 0;
        var logUrl = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/build/builds/{buildId}/logs/{lastLogId}?api-version={ApiVersion}";
        var logResponse = await Http.GetAsync(logUrl);
        await EnsureSuccessAsync(logResponse, $"get build log {lastLogId}");

        return await logResponse.Content.ReadAsStringAsync();
    }

    public async Task<List<object>> GetBuildTimelineAsync(string project, int buildId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/build/builds/{buildId}/timeline?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get build timeline for {buildId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["records"]?.AsArray()
            .Where(r => r?["type"]?.GetValue<string>() == "Task")
            .Select(r => (object)new
            {
                name = r?["name"]?.GetValue<string>(),
                state = r?["state"]?.GetValue<string>(),
                result = r?["result"]?.GetValue<string>(),
                startTime = r?["startTime"]?.GetValue<string>(),
                finishTime = r?["finishTime"]?.GetValue<string>(),
                errorCount = r?["errorCount"]?.GetValue<int>(),
                warningCount = r?["warningCount"]?.GetValue<int>(),
                log = r?["log"]?["url"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task CancelBuildAsync(string project, int buildId)
    {
        _logger.LogInformation("Cancelling build {BuildId}", buildId);

        var body = JsonSerializer.Serialize(new { status = "cancelling" });
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/build/builds/{buildId}?api-version={ApiVersion}";
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"cancel build {buildId}");
    }

    public async Task<List<object>> GetBuildArtifactsAsync(string project, int buildId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/build/builds/{buildId}/artifacts?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get artifacts for build {buildId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(a => (object)new
            {
                id = a?["id"]?.GetValue<int>(),
                name = a?["name"]?.GetValue<string>(),
                source = a?["source"]?.GetValue<string>(),
                downloadUrl = a?["resource"]?["downloadUrl"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    // ─── Variable Group Operations ─────────────────────────────────────

    public async Task<List<object>> GetVariableGroupsAsync(string project)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/distributedtask/variablegroups?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list variable groups in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(g =>
            {
                var variables = g?["variables"]?.AsObject()
                    .Select(kv => new
                    {
                        name = kv.Key,
                        isSecret = kv.Value?["isSecret"]?.GetValue<bool>() ?? false,
                        value = (kv.Value?["isSecret"]?.GetValue<bool>() ?? false) ? "***" : kv.Value?["value"]?.GetValue<string>(),
                    })
                    .ToList();

                return (object)new
                {
                    id = g?["id"]?.GetValue<int>(),
                    name = g?["name"]?.GetValue<string>(),
                    description = g?["description"]?.GetValue<string>(),
                    variableCount = variables?.Count ?? 0,
                    variables,
                };
            })
            .ToList() ?? [];
    }

    public async Task AddVariableToGroupAsync(
        string project, int groupId, string variableName, string variableValue, bool isSecret = false)
    {
        _logger.LogInformation("Adding variable '{Name}' to group {GroupId} (secret: {IsSecret})",
            variableName, groupId, isSecret);

        // First get the current group
        var getUrl = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/distributedtask/variablegroups/{groupId}?api-version={ApiVersion}";
        var getResponse = await Http.GetAsync(getUrl);
        await EnsureSuccessAsync(getResponse, $"get variable group {groupId}");

        var group = JsonNode.Parse(await getResponse.Content.ReadAsStringAsync());
        if (group is null) throw new InvalidOperationException($"Variable group {groupId} not found.");

        // Add/update the variable
        var variables = group["variables"]?.AsObject() ?? new JsonObject();
        variables[variableName] = JsonNode.Parse(JsonSerializer.Serialize(new { value = variableValue, isSecret }));
        group["variables"] = variables;

        // Update the group
        var putUrl = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/distributedtask/variablegroups/{groupId}?api-version={ApiVersion}";
        var request = new HttpRequestMessage(HttpMethod.Put, putUrl)
        {
            Content = new StringContent(group.ToJsonString(), Encoding.UTF8, "application/json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"update variable group {groupId}");
    }

    // ─── Agent Pool Operations ─────────────────────────────────────────

    public async Task<List<object>> GetAgentPoolsAsync()
    {
        var url = $"{OrgUrl}/_apis/distributedtask/pools?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, "list agent pools");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(p => (object)new
            {
                id = p?["id"]?.GetValue<int>(),
                name = p?["name"]?.GetValue<string>(),
                poolType = p?["poolType"]?.GetValue<string>(),
                size = p?["size"]?.GetValue<int>(),
                isHosted = p?["isHosted"]?.GetValue<bool>(),
            })
            .ToList() ?? [];
    }

    // ─── Test Results for Builds ───────────────────────────────────────

    public async Task<List<object>> GetTestResultsForBuildAsync(
        string project, int buildId, string? outcomeFilter = null)
    {
        // First get test runs for the build
        var runsUrl = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/runs?buildUri=vstfs:///Build/Build/{buildId}&api-version={ApiVersion}";
        var runsResponse = await Http.GetAsync(runsUrl);
        await EnsureSuccessAsync(runsResponse, $"get test runs for build {buildId}");

        var runsJson = JsonNode.Parse(await runsResponse.Content.ReadAsStringAsync());
        var runs = runsJson?["value"]?.AsArray();
        if (runs is null || runs.Count == 0) return [];

        var allResults = new List<object>();
        foreach (var run in runs)
        {
            var runId = run?["id"]?.GetValue<int>() ?? 0;
            if (runId == 0) continue;

            var filter = outcomeFilter is not null ? $"&outcomes={outcomeFilter}" : "";
            var resultsUrl = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/results?$top=200{filter}&api-version={ApiVersion}";
            var resultsResponse = await Http.GetAsync(resultsUrl);
            if (!resultsResponse.IsSuccessStatusCode) continue;

            var resultsJson = JsonNode.Parse(await resultsResponse.Content.ReadAsStringAsync());
            var results = resultsJson?["value"]?.AsArray();
            if (results is null) continue;

            foreach (var r in results)
            {
                allResults.Add(new
                {
                    testCaseName = r?["testCaseTitle"]?.GetValue<string>(),
                    outcome = r?["outcome"]?.GetValue<string>(),
                    durationInMs = r?["durationInMs"]?.GetValue<double>(),
                    errorMessage = r?["errorMessage"]?.GetValue<string>(),
                    stackTrace = r?["stackTrace"]?.GetValue<string>(),
                    runId,
                });
            }
        }

        return allResults;
    }

    // ─── Wiki Operations ───────────────────────────────────────────────

    public async Task<List<object>> GetWikisAsync(string project)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wiki/wikis?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list wikis in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(w => (object)new
            {
                id = w?["id"]?.GetValue<string>(),
                name = w?["name"]?.GetValue<string>(),
                type = w?["type"]?.GetValue<string>(),
                url = w?["url"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<(string? Content, string? Version)> GetWikiPageAsync(
        string project, string wikiId, string pagePath)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wiki/wikis/{Uri.EscapeDataString(wikiId)}/pages?path={Uri.EscapeDataString(pagePath)}&includeContent=true&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return (null, null);
        await EnsureSuccessAsync(response, $"get wiki page '{pagePath}'");

        var etag = response.Headers.ETag?.Tag;
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var content = json?["content"]?.GetValue<string>();
        var version = etag ?? json?["eTag"]?.GetValue<string>();

        return (content, version);
    }

    public async Task<string> CreateWikiPageAsync(
        string project, string wikiId, string pagePath, string content)
    {
        _logger.LogInformation("Creating wiki page '{Path}' in wiki '{WikiId}'", pagePath, wikiId);

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wiki/wikis/{Uri.EscapeDataString(wikiId)}/pages?path={Uri.EscapeDataString(pagePath)}&api-version={ApiVersion}";
        var body = JsonSerializer.Serialize(new { content });
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"create wiki page '{pagePath}'");

        var etag = response.Headers.ETag?.Tag ?? "unknown";
        return etag;
    }

    public async Task<string> UpdateWikiPageAsync(
        string project, string wikiId, string pagePath, string content, string version)
    {
        _logger.LogInformation("Updating wiki page '{Path}' in wiki '{WikiId}'", pagePath, wikiId);

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wiki/wikis/{Uri.EscapeDataString(wikiId)}/pages?path={Uri.EscapeDataString(pagePath)}&api-version={ApiVersion}";
        var body = JsonSerializer.Serialize(new { content });
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", version);
        var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response, $"update wiki page '{pagePath}'");

        var etag = response.Headers.ETag?.Tag ?? "unknown";
        return etag;
    }

    public async Task DeleteWikiPageAsync(string project, string wikiId, string pagePath)
    {
        _logger.LogInformation("Deleting wiki page '{Path}' from wiki '{WikiId}'", pagePath, wikiId);

        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wiki/wikis/{Uri.EscapeDataString(wikiId)}/pages?path={Uri.EscapeDataString(pagePath)}&api-version={ApiVersion}";
        var response = await Http.DeleteAsync(url);
        await EnsureSuccessAsync(response, $"delete wiki page '{pagePath}'");
    }

    // ─── Test Plan Operations ──────────────────────────────────────────

    public async Task<List<object>> GetTestPlansAsync(string project)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/plans?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list test plans in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(p => (object)new
            {
                id = p?["id"]?.GetValue<int>(),
                name = p?["name"]?.GetValue<string>(),
                state = p?["state"]?.GetValue<string>(),
                startDate = p?["startDate"]?.GetValue<string>(),
                endDate = p?["endDate"]?.GetValue<string>(),
                owner = p?["owner"]?["displayName"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<List<object>> GetTestSuitesAsync(string project, int planId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/plans/{planId}/suites?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list test suites in plan {planId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(s => (object)new
            {
                id = s?["id"]?.GetValue<int>(),
                name = s?["name"]?.GetValue<string>(),
                suiteType = s?["suiteType"]?.GetValue<string>(),
                testCaseCount = s?["testCaseCount"]?.GetValue<int>(),
                parentSuiteId = s?["parentSuite"]?["id"]?.GetValue<int>(),
            })
            .ToList() ?? [];
    }

    public async Task<List<object>> GetTestCasesAsync(string project, int planId, int suiteId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/plans/{planId}/suites/{suiteId}/testcases?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list test cases in suite {suiteId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(tc => (object)new
            {
                testCaseId = tc?["testCase"]?["id"]?.GetValue<string>(),
                testCaseName = tc?["testCase"]?["name"]?.GetValue<string>(),
                pointAssignments = tc?["pointAssignments"]?.AsArray()
                    .Select(pa => new
                    {
                        tester = pa?["tester"]?["displayName"]?.GetValue<string>(),
                        configuration = pa?["configuration"]?["name"]?.GetValue<string>(),
                    })
                    .ToList(),
            })
            .ToList() ?? [];
    }

    public async Task<List<object>> GetTestRunsAsync(string project, int? buildId = null, int top = 25)
    {
        var buildFilter = buildId.HasValue ? $"&buildUri=vstfs:///Build/Build/{buildId.Value}" : "";
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/runs?$top={top}{buildFilter}&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list test runs in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(r => (object)new
            {
                id = r?["id"]?.GetValue<int>(),
                name = r?["name"]?.GetValue<string>(),
                state = r?["state"]?.GetValue<string>(),
                totalTests = r?["totalTests"]?.GetValue<int>(),
                passedTests = r?["passedTests"]?.GetValue<int>(),
                failedTests = r?["unanalyzedTests"]?.GetValue<int>(),
                startedDate = r?["startedDate"]?.GetValue<string>(),
                completedDate = r?["completedDate"]?.GetValue<string>(),
                buildId = r?["build"]?["id"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<List<object>> GetTestResultsAsync(string project, int runId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/runs/{runId}/results?$top=200&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get test results for run {runId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(r => (object)new
            {
                testCaseName = r?["testCaseTitle"]?.GetValue<string>(),
                outcome = r?["outcome"]?.GetValue<string>(),
                durationInMs = r?["durationInMs"]?.GetValue<double>(),
                errorMessage = r?["errorMessage"]?.GetValue<string>(),
                stackTrace = r?["stackTrace"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<string> GetCodeCoverageAsync(string project, int buildId)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/test/codecoverage?buildId={buildId}&api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get code coverage for build {buildId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var coverageData = json?["coverageData"]?.AsArray();
        if (coverageData is null || coverageData.Count == 0) return string.Empty;

        var result = new System.Text.StringBuilder();
        foreach (var data in coverageData)
        {
            var modules = data?["coverageStats"]?.AsArray();
            if (modules is null) continue;
            foreach (var stat in modules)
            {
                result.AppendLine($"{stat?["label"]?.GetValue<string>()}: " +
                    $"{stat?["covered"]?.GetValue<int>()}/{stat?["total"]?.GetValue<int>()} covered");
            }
        }
        return result.ToString();
    }

    // ─── Organization / Cross-Project Operations ───────────────────────

    public async Task<List<object>> GetTeamsAsync(string project)
    {
        var url = $"{OrgUrl}/_apis/projects/{Uri.EscapeDataString(project)}/teams?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list teams in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(t => (object)new
            {
                id = t?["id"]?.GetValue<string>(),
                name = t?["name"]?.GetValue<string>(),
                description = t?["description"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<List<object>> GetServiceConnectionsAsync(string project)
    {
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/serviceendpoint/endpoints?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"list service connections in '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(e => (object)new
            {
                id = e?["id"]?.GetValue<string>(),
                name = e?["name"]?.GetValue<string>(),
                type = e?["type"]?.GetValue<string>(),
                url = e?["url"]?.GetValue<string>(),
                isReady = e?["isReady"]?.GetValue<bool>(),
                createdBy = e?["createdBy"]?["displayName"]?.GetValue<string>(),
            })
            .ToList() ?? [];
    }

    public async Task<List<object>> GetWorkItemRevisionsAsync(int workItemId)
    {
        var url = $"{OrgUrl}/_apis/wit/workitems/{workItemId}/revisions?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get revisions for work item {workItemId}");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(r =>
            {
                var fields = r?["fields"];
                return (object)new
                {
                    rev = r?["rev"]?.GetValue<int>(),
                    changedBy = fields?["System.ChangedBy"]?["displayName"]?.GetValue<string>()
                                ?? fields?["System.ChangedBy"]?.GetValue<string>(),
                    changedDate = fields?["System.ChangedDate"]?.GetValue<string>(),
                    state = fields?["System.State"]?.GetValue<string>(),
                    title = fields?["System.Title"]?.GetValue<string>(),
                    assignedTo = fields?["System.AssignedTo"]?["displayName"]?.GetValue<string>(),
                };
            })
            .ToList() ?? [];
    }

    public async Task<List<WorkItemSummary>> CrossProjectSearchAsync(string wiqlWhereClause, int top = 50)
    {
        _logger.LogInformation("Cross-project search: {Where}", wiqlWhereClause);

        // Cross-project WIQL query — use the org-level endpoint (no project in path)
        var query = $@"SELECT [System.Id] FROM WorkItems WHERE {wiqlWhereClause} ORDER BY [System.ChangedDate] DESC";
        var body = JsonSerializer.Serialize(new { query });
        var url = $"{OrgUrl}/_apis/wit/wiql?$top={top}&api-version={ApiVersion}";

        var response = await Http.PostAsync(url,
            new StringContent(body, Encoding.UTF8, "application/json"));
        await EnsureSuccessAsync(response, "cross-project WIQL query");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var ids = json?["workItems"]?.AsArray()
            .Select(wi => wi?["id"]?.GetValue<int>())
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToArray() ?? [];

        if (ids.Length == 0) return [];
        return await GetWorkItemsByIdsAsync(ids);
    }

    // ─── PR Mapping ────────────────────────────────────────────────────

    private static PullRequestSummary MapToPullRequestSummary(JsonNode pr)
    {
        return new PullRequestSummary
        {
            Id = pr["pullRequestId"]?.GetValue<int>() ?? 0,
            Title = pr["title"]?.GetValue<string>() ?? string.Empty,
            Description = pr["description"]?.GetValue<string>(),
            Status = pr["status"]?.GetValue<string>() ?? string.Empty,
            SourceBranch = pr["sourceRefName"]?.GetValue<string>()?.Replace("refs/heads/", "") ?? string.Empty,
            TargetBranch = pr["targetRefName"]?.GetValue<string>()?.Replace("refs/heads/", "") ?? string.Empty,
            CreatedBy = pr["createdBy"]?["displayName"]?.GetValue<string>(),
            Reviewers = pr["reviewers"]?.AsArray()
                .Select(r => r?["displayName"]?.GetValue<string>() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList(),
            Url = pr["url"]?.GetValue<string>(),
            MergeStatus = pr["mergeStatus"]?.GetValue<string>(),
            CreationDate = pr["creationDate"]?.GetValue<DateTime?>(),
            ClosedDate = pr["closedDate"]?.GetValue<DateTime?>(),
            Repository = pr["repository"]?["name"]?.GetValue<string>(),
            Labels = pr["labels"]?.AsArray()
                .Select(l => l?["name"]?.GetValue<string>() ?? "")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList(),
        };
    }

    public void Dispose() => _http?.Dispose();
}

