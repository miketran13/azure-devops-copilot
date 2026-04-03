using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using DevOpsCopilot.Models;
using DevOpsCopilot.Models.Configuration;

namespace DevOpsCopilot.Functions;

/// <summary>
/// Endpoint to list available AI models for the frontend model selector.
/// </summary>
public sealed class ModelFunction
{
    private readonly AzureOpenAIConfiguration _openAIConfig;
    private readonly GitHubModelsConfiguration _githubConfig;
    private readonly string _aiProvider;

    public ModelFunction(
        IOptions<AzureOpenAIConfiguration> openAIConfig,
        IOptions<GitHubModelsConfiguration> githubConfig,
        IConfiguration configuration)
    {
        _openAIConfig = openAIConfig.Value;
        _githubConfig = githubConfig.Value;
        _aiProvider = configuration.GetValue<string>("AIProvider") ?? "AzureOpenAI";
    }

    /// <summary>
    /// GET /api/models — returns the list of available models for the active AI provider.
    /// </summary>
    [Function("ListModels")]
    public IActionResult ListModels(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "models")] HttpRequest req)
    {
        if (string.Equals(_aiProvider, "GitHubModels", StringComparison.OrdinalIgnoreCase))
            return new OkObjectResult(GetGitHubModels());

        return new OkObjectResult(GetAzureOpenAIModels());
    }

    private List<ModelInfo> GetGitHubModels()
    {
        if (_githubConfig.Models.Count > 0)
        {
            return _githubConfig.Models.Select(m => new ModelInfo
            {
                Id = m.Id,
                DisplayName = m.DisplayName,
                Description = m.Description,
                IsDefault = m.IsDefault,
            }).ToList();
        }

        // Fall back to the single default model
        return [new ModelInfo
        {
            Id = _githubConfig.DefaultModel,
            DisplayName = _githubConfig.DefaultModel,
            Description = "Default GitHub model",
            IsDefault = true,
        }];
    }

    private List<ModelInfo> GetAzureOpenAIModels()
    {
        var models = _openAIConfig.Models.Select(m => new ModelInfo
        {
            Id = m.Id,
            DisplayName = m.DisplayName,
            Description = m.Description,
            IsDefault = m.IsDefault,
        }).ToList();

        if (models.Count == 0)
        {
            models.Add(new ModelInfo
            {
                Id = _openAIConfig.DefaultDeployment,
                DisplayName = _openAIConfig.DefaultDeployment,
                Description = "Default model",
                IsDefault = true,
            });
        }

        return models;
    }
}
