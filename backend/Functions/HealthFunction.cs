using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace DevOpsCopilot.Functions;

/// <summary>
/// Health check endpoint for monitoring and deployment validation.
/// </summary>
public sealed class HealthFunction
{
    private readonly ILogger<HealthFunction> _logger;

    public HealthFunction(ILogger<HealthFunction> logger)
    {
        _logger = logger;
    }

    [Function("Health")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        _logger.LogInformation("Health check requested");

        return new OkObjectResult(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = typeof(HealthFunction).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        });
    }
}
