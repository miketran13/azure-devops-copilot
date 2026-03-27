using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevOpsCopilot.Agents;
using DevOpsCopilot.Models.Configuration;
using DevOpsCopilot.Services;
using DevOpsCopilot.Services.Memory;
using DevOpsCopilot.Tools;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// ─── External configuration files ──────────────────────────────────
builder.Configuration.AddJsonFile("Config/prompts.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("Config/tools.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("Config/custom-fields.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile("Config/memory.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile("Config/mcp.json", optional: true, reloadOnChange: true);

// Re-add environment variables so Azure App Settings (e.g. Memory__Provider)
// take precedence over the JSON config files above.
builder.Configuration.AddEnvironmentVariables();

// Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// CORS (also configured via host.json for Functions runtime)
// Allowed origins are read from Cors__AllowedOrigins (comma-separated).
// In Azure, set the app setting to include your publisher's gallery CDN origins.
// Defaults cover local dev and standard ADO origins when the setting is absent.
var corsOrigins = (builder.Configuration["Cors__AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length == 0)
{
    corsOrigins = [
        "https://dev.azure.com",
        "https://*.visualstudio.com",
        "https://localhost:3000",
    ];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AzureDevOps", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── Configuration binding ─────────────────────────────────────────
builder.Services.Configure<PromptConfiguration>(builder.Configuration);
builder.Services.Configure<ToolConfiguration>(builder.Configuration);
builder.Services.Configure<CustomFieldConfiguration>(builder.Configuration);
builder.Services.Configure<MemoryConfiguration>(builder.Configuration.GetSection("memory"));
builder.Services.Configure<McpConfiguration>(builder.Configuration);
builder.Services.Configure<AzureOpenAIConfiguration>(builder.Configuration.GetSection("AzureOpenAI"));

// ─── Caching ───────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ─── Services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<TokenValidationService>();
builder.Services.AddSingleton<PromptConfigurationService>();
builder.Services.AddSingleton<CustomFieldService>();
builder.Services.AddSingleton<MappingService>();
builder.Services.AddScoped<AzureDevOpsService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<WorkItemRelationshipService>();
builder.Services.AddScoped<AgentFactory>();
builder.Services.AddScoped<AgentOrchestrator>();

// ─── Tool providers (auto-register all IToolProvider implementations) ──
var toolProviderTypes = typeof(IToolProvider).Assembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false }
             && typeof(IToolProvider).IsAssignableFrom(t));

foreach (var type in toolProviderTypes)
{
    builder.Services.AddSingleton(typeof(IToolProvider), type);
}

// ─── Session/memory store ──────────────────────────────────────────
var memoryEnabled = builder.Configuration.GetValue<bool>("memory:enabled");
var memoryProvider = builder.Configuration.GetValue<string>("memory:provider") ?? "inMemory";

if (!memoryEnabled)
{
    builder.Services.AddSingleton<ISessionStore, NullSessionStore>();
}
else
{
    builder.Services.AddSingleton<ISessionStore>(memoryProvider.ToLowerInvariant() switch
    {
        "inmemory" => new InMemorySessionStore(),
        "localfile" => new LocalFileSessionStore(
            builder.Configuration.GetValue<string>("memory:localFilePath")
                // /tmp is the only writable directory on Linux Consumption plans.
                // AppContext.BaseDirectory points to wwwroot which is read-only.
                ?? Path.Combine(Path.GetTempPath(), "sessions")),
        "azuretablestorage" => new AzureTableSessionStore(
            builder.Configuration.GetValue<string>("memory:azureTableConnectionString")
                ?? throw new InvalidOperationException("memory:azureTableConnectionString is required when using azuretablestorage provider")),
        // Future: "cosmosdb" => new CosmosDbSessionStore(...)
        _ => new InMemorySessionStore(),
    });
}

var host = builder.Build();

// ─── Startup validation ────────────────────────────────────────────
// Fail fast if critical configuration is missing. This prevents the
// app from starting in a broken state and producing confusing errors.
var config = host.Services.GetRequiredService<IConfiguration>();
var startupErrors = new List<string>();

if (string.IsNullOrEmpty(config["AzureOpenAI:Endpoint"]))
    startupErrors.Add("AzureOpenAI:Endpoint is required. Set it in local.settings.json or Azure App Settings.");

if (string.IsNullOrEmpty(config["AzureDevOps:DefaultOrganizationUrl"]))
    startupErrors.Add("AzureDevOps:DefaultOrganizationUrl is required. Set it in local.settings.json or Azure App Settings.");

var hasDeployment = !string.IsNullOrEmpty(config["AzureOpenAI:DefaultDeployment"])
    || config.GetSection("AzureOpenAI:Models").GetChildren().Any();
if (!hasDeployment)
    startupErrors.Add("No Azure OpenAI model deployment configured. Set AzureOpenAI:DefaultDeployment or AzureOpenAI:Models.");

if (startupErrors.Count > 0)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    foreach (var error in startupErrors)
        logger.LogCritical("Startup validation failed: {Error}", error);
    throw new InvalidOperationException(
        $"Application startup failed due to {startupErrors.Count} configuration error(s):\n" +
        string.Join("\n", startupErrors.Select(e => $"  - {e}")));
}

host.Run();
