using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Functions;

file sealed record HealthChecks(
    string AzureOpenAI,
    string AzureOpenAIAuth,
    string AzureDevOps,
    string ModelDeployment,
    string ExtensionSecurity);

file sealed record HealthResponse(
    string Status,
    DateTime Timestamp,
    string Version,
    bool ConfigurationComplete,
    HealthChecks Checks);

/// <summary>
/// Health check endpoint for monitoring and deployment validation.
/// Distinguishes between "host is up" and "AI service is operational".
/// </summary>
public sealed class HealthFunction
{
    private readonly ILogger<HealthFunction> _logger;
    private readonly IConfiguration _configuration;

    public HealthFunction(ILogger<HealthFunction> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        _logger.LogInformation("Health check requested");

        var hasOpenAIEndpoint = !string.IsNullOrEmpty(_configuration["AzureOpenAI:Endpoint"]);
        var hasOpenAIKey = !string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]);
        var hasDeployment = !string.IsNullOrEmpty(_configuration["AzureOpenAI:DefaultDeployment"])
            || _configuration.GetSection("AzureOpenAI:Models").GetChildren().Any();
        var hasOrgUrl = !string.IsNullOrEmpty(_configuration["AzureDevOps:DefaultOrganizationUrl"]);
        var hasSharedSecret = !string.IsNullOrEmpty(_configuration["Extension:SharedSecret"]);

        var aiReady = hasOpenAIEndpoint && hasDeployment;
        var devOpsReady = hasOrgUrl;
        var securityConfigured = hasSharedSecret;
        var configurationComplete = aiReady && devOpsReady;

        // Determine status: healthy/degraded/unhealthy
        string status;
        if (aiReady && devOpsReady)
            status = "healthy";
        else if (!devOpsReady)
            status = "unhealthy";
        else
            status = "degraded";

        return new OkObjectResult(new HealthResponse(
            Status: status,
            Timestamp: DateTime.UtcNow,
            Version: typeof(HealthFunction).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            ConfigurationComplete: configurationComplete,
            Checks: new HealthChecks(
                AzureOpenAI: aiReady ? "configured" : "missing",
                AzureOpenAIAuth: hasOpenAIKey ? "api-key" : "default-credential",
                AzureDevOps: devOpsReady ? "configured" : "missing",
                ModelDeployment: hasDeployment ? "configured" : "missing",
                ExtensionSecurity: securityConfigured ? "configured" : "disabled (dev mode)"
            )
        ));
    }
}
