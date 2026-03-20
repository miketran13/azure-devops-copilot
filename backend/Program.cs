using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DevOpsCopilot.Services;
using DevOpsCopilot.Agents;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Application Insights
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// CORS (also configured via host.json for Functions runtime)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AzureDevOps", policy =>
    {
        policy.WithOrigins(
                "https://dev.azure.com",
                "https://*.visualstudio.com",
                "https://localhost:3000")   // local dev server
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Register services
builder.Services.AddSingleton<TokenValidationService>();
builder.Services.AddScoped<AzureDevOpsService>();
builder.Services.AddScoped<AgentOrchestrator>();

builder.Build().Run();
