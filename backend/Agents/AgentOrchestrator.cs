using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DevOpsCopilot.Models;
using DevOpsCopilot.Services;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace DevOpsCopilot.Agents;

/// <summary>
/// Manages the lifecycle of the multi-agent system for each chat request.
/// Creates scoped agent instances with the user's Azure DevOps credentials.
/// </summary>
public sealed class AgentOrchestrator
{
    private readonly IConfiguration _configuration;
    private readonly AzureDevOpsService _devOpsService;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IConfiguration configuration,
        AzureDevOpsService devOpsService,
        ILogger<AgentOrchestrator> logger)
    {
        _configuration = configuration;
        _devOpsService = devOpsService;
        _logger = logger;
    }

    /// <summary>
    /// Process a chat message through the multi-agent system.
    /// </summary>
    public async Task<Models.ChatResponse> ProcessMessageAsync(
        ChatRequest request,
        string userAccessToken)
    {
        // Initialize Azure DevOps service with user's token
        _devOpsService.Initialize(userAccessToken, request.OrganizationUrl);

        // Create Azure OpenAI client
        var openAIClient = CreateOpenAIClient();
        var deploymentName = _configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName not configured.");

        // Create specialist agents
        var searchAgent = AgentFactory.CreateSearchAgent(openAIClient, deploymentName, _devOpsService);
        var writerAgent = AgentFactory.CreateWriterAgent(openAIClient, deploymentName, _devOpsService);
        var analystAgent = AgentFactory.CreateAnalystAgent(openAIClient, deploymentName, _devOpsService);

        // Create orchestrator that coordinates specialists
        var orchestrator = AgentFactory.CreateOrchestratorAgent(
            openAIClient, deploymentName, searchAgent, writerAgent, analystAgent);

        _logger.LogInformation("Processing message through orchestrator agent");

        try
        {
            // Build the full message list including conversation history
            var messages = BuildMessageList(request);

            // Run the orchestrator agent with full conversation context
            var session = await orchestrator.CreateSessionAsync();
            var response = await orchestrator.RunAsync(messages, session);

            return new Models.ChatResponse
            {
                Reply = response?.ToString() ?? "I wasn't able to process your request. Please try again.",
                SuggestedActions = GenerateSuggestedActions(request.Message)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message through agent orchestrator");

            return new Models.ChatResponse
            {
                Reply = $"An error occurred while processing your request: {ex.Message}. " +
                        "Please check your Azure DevOps permissions and try again.",
                SuggestedActions = ["Try rephrasing your request", "Check your connection settings"]
            };
        }
    }

    /// <summary>
    /// Process a message with streaming response.
    /// </summary>
    public async IAsyncEnumerable<string> ProcessMessageStreamingAsync(
        ChatRequest request,
        string userAccessToken)
    {
        // Initialize Azure DevOps service with user's token
        _devOpsService.Initialize(userAccessToken, request.OrganizationUrl);

        var openAIClient = CreateOpenAIClient();
        var deploymentName = _configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName not configured.");

        var searchAgent = AgentFactory.CreateSearchAgent(openAIClient, deploymentName, _devOpsService);
        var writerAgent = AgentFactory.CreateWriterAgent(openAIClient, deploymentName, _devOpsService);
        var analystAgent = AgentFactory.CreateAnalystAgent(openAIClient, deploymentName, _devOpsService);

        var orchestrator = AgentFactory.CreateOrchestratorAgent(
            openAIClient, deploymentName, searchAgent, writerAgent, analystAgent);

        var messages = BuildMessageList(request);

        var session = await orchestrator.CreateSessionAsync();
        await foreach (var update in orchestrator.RunStreamingAsync(messages, session))
        {
            var text = update?.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }
    }

    private AzureOpenAIClient CreateOpenAIClient()
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured.");

        var apiKey = _configuration["AzureOpenAI:ApiKey"];

        if (!string.IsNullOrEmpty(apiKey))
        {
            // Use API key authentication (development)
            return new AzureOpenAIClient(
                new Uri(endpoint),
                new System.ClientModel.ApiKeyCredential(apiKey));
        }

        // Use Managed Identity / DefaultAzureCredential (production)
        return new AzureOpenAIClient(
            new Uri(endpoint),
            new DefaultAzureCredential());
    }

    /// <summary>
    /// Converts the frontend conversation history + current message into a list of
    /// ChatMessage objects that the Agent Framework understands and passes to the LLM.
    /// This gives the orchestrator full multi-turn context so it can handle follow-ups,
    /// confirmations ("yes, create it"), and references to earlier messages.
    /// </summary>
    private static IEnumerable<ChatMessage> BuildMessageList(ChatRequest request)
    {
        var messages = new List<ChatMessage>();

        // Replay prior conversation turns so the agent sees full context
        if (request.ConversationHistory is { Count: > 0 } history)
        {
            foreach (var msg in history)
            {
                var role = msg.Role?.ToLowerInvariant() switch
                {
                    "assistant" => ChatRole.Assistant,
                    "system" => ChatRole.System,
                    _ => ChatRole.User,
                };
                messages.Add(new ChatMessage(role, msg.Content));
            }
        }

        // Build the current user message with project context prefix
        var currentMessage = string.IsNullOrEmpty(request.ProjectName)
            ? request.Message
            : $"[Context: Azure DevOps project = \"{request.ProjectName}\"]\n{request.Message}";

        messages.Add(new ChatMessage(ChatRole.User, currentMessage));

        return messages;
    }

    private static List<string> GenerateSuggestedActions(string userMessage)
    {
        var lowerMessage = userMessage.ToLowerInvariant();

        if (lowerMessage.Contains("bug") || lowerMessage.Contains("issue"))
            return ["Show me more bugs", "Create a new bug", "Analyze bug trends"];

        if (lowerMessage.Contains("user story") || lowerMessage.Contains("story"))
            return ["Show active stories", "Create a user story", "Analyze story quality"];

        if (lowerMessage.Contains("create") || lowerMessage.Contains("new"))
            return ["Search existing items", "Create another item", "View recent items"];

        if (lowerMessage.Contains("analyz") || lowerMessage.Contains("review"))
            return ["Generate test cases", "Suggest improvements", "Check other items"];

        return ["Search work items", "Create a work item", "Analyze requirements"];
    }
}
