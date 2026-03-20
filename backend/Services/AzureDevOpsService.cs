using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevOpsCopilot.Models;
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
    private readonly string _defaultOrgUrl;
    private readonly string? _configuredPat;

    private HttpClient? _http;
    private string? _orgUrl;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AzureDevOpsService(IConfiguration configuration, ILogger<AzureDevOpsService> logger)
    {
        _logger = logger;
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
                  && !_configuredPat.StartsWith("YOUR-", StringComparison.OrdinalIgnoreCase);
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
        string? tags = null, int? priority = null)
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
        var url = $"{OrgUrl}/{Uri.EscapeDataString(project)}/_apis/wit/workitemtypes?api-version={ApiVersion}";
        var response = await Http.GetAsync(url);
        await EnsureSuccessAsync(response, $"get work item types for '{project}'");

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync());
        return json?["value"]?.AsArray()
            .Select(t => t?["name"]?.GetValue<string>())
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList() ?? [];
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private static object Op(string op, string path, object value) =>
        new { op, path, value };

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"ADO API error ({(int)response.StatusCode} {response.StatusCode}) during {operation}: {body}");
    }

    private static WorkItemSummary MapToSummary(JsonNode wi)
    {
        var fields = wi["fields"];
        string? F(string name) => fields?[name]?.GetValue<string>();
        int? FInt(string name) => fields?[name]?.GetValue<int?>() ?? null;
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
            CreatedDate = FDate("System.CreatedDate"),
            ChangedDate = FDate("System.ChangedDate"),
            Url = wi["url"]?.GetValue<string>(),
        };
    }

    public void Dispose() => _http?.Dispose();
}

