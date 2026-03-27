using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using DevOpsCopilot.Models;
using DevOpsCopilot.Models.Configuration;

namespace DevOpsCopilot.Functions;

/// <summary>
/// Endpoint to list available AI models for the frontend model selector.
/// </summary>
public sealed class ModelFunction
{
    private readonly AzureOpenAIConfiguration _config;

    public ModelFunction(IOptions<AzureOpenAIConfiguration> config)
    {
        _config = config.Value;
    }

    /// <summary>
    /// GET /api/models — returns the list of available models.
    /// </summary>
    [Function("ListModels")]
    public IActionResult ListModels(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "models")] HttpRequest req)
    {
        var models = _config.Models.Select(m => new ModelInfo
        {
            Id = m.Id,
            DisplayName = m.DisplayName,
            Description = m.Description,
            IsDefault = m.IsDefault,
        }).ToList();

        // If no models configured, return the default deployment as a single model
        if (models.Count == 0)
        {
            models.Add(new ModelInfo
            {
                Id = _config.DefaultDeployment,
                DisplayName = _config.DefaultDeployment,
                Description = "Default model",
                IsDefault = true,
            });
        }

        return new OkObjectResult(models);
    }
}
