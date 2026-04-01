using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using DevOpsCopilot.Models.Configuration;

namespace DevOpsCopilot.Services.Providers;

/// <summary>
/// Provides AI chat clients backed by Azure OpenAI.
/// Extracted from AgentOrchestrator.CreateOpenAIClient() — same logic, now behind IChatClientProvider.
/// </summary>
public sealed class AzureOpenAIChatClientProvider : IChatClientProvider
{
    private readonly IConfiguration _configuration;
    private readonly AzureOpenAIConfiguration _openAIConfig;
    private readonly AzureOpenAIClient _client;

    public AzureOpenAIChatClientProvider(
        IConfiguration configuration,
        IOptions<AzureOpenAIConfiguration> openAIConfig)
    {
        _configuration = configuration;
        _openAIConfig = openAIConfig.Value;
        _client = CreateClient();
    }

    public IChatClient GetChatClient(string modelOrDeployment)
    {
        return _client
            .GetChatClient(modelOrDeployment)
            .AsIChatClient();
    }

    private AzureOpenAIClient CreateClient()
    {
        var endpoint = !string.IsNullOrEmpty(_openAIConfig.Endpoint)
            ? _openAIConfig.Endpoint
            : _configuration["AzureOpenAI:Endpoint"]
              ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured.");

        var apiKey = !string.IsNullOrEmpty(_openAIConfig.ApiKey)
            ? _openAIConfig.ApiKey
            : _configuration["AzureOpenAI:ApiKey"];

        if (!string.IsNullOrEmpty(apiKey))
        {
            return new AzureOpenAIClient(
                new Uri(endpoint),
                new System.ClientModel.ApiKeyCredential(apiKey));
        }

        return new AzureOpenAIClient(
            new Uri(endpoint),
            new DefaultAzureCredential());
    }
}
