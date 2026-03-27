using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DevOpsCopilot.Models;
using DevOpsCopilot.Models.Configuration;
using DevOpsCopilot.Services;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace DevOpsCopilot.Agents;

/// <summary>
/// Manages the lifecycle of the multi-agent system for each chat request.
/// Creates scoped agent instances with the user's Azure DevOps credentials.
/// Supports streaming responses with processing-step events so the frontend
/// can show what the system is doing in real time.
/// </summary>
public sealed class AgentOrchestrator
{
    private readonly IConfiguration _configuration;
    private readonly AzureOpenAIConfiguration _openAIConfig;
    private readonly AzureDevOpsService _devOpsService;
    private readonly AgentFactory _agentFactory;
    private readonly PromptConfigurationService _promptService;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        IConfiguration configuration,
        IOptions<AzureOpenAIConfiguration> openAIConfig,
        AzureDevOpsService devOpsService,
        AgentFactory agentFactory,
        PromptConfigurationService promptService,
        ILogger<AgentOrchestrator> logger)
    {
        _configuration = configuration;
        _openAIConfig = openAIConfig.Value;
        _devOpsService = devOpsService;
        _agentFactory = agentFactory;
        _promptService = promptService;
        _logger = logger;
    }

    /// <summary>
    /// Resolve the deployment name from the model ID (or use default).
    /// </summary>
    private string ResolveDeploymentName(string? modelId)
    {
        if (!string.IsNullOrEmpty(modelId))
        {
            var match = _openAIConfig.Models
                .FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.DeploymentName;

            _logger.LogWarning("Requested model '{ModelId}' not found, using default", modelId);
        }

        // Fallback: default model from Models list, then DefaultDeployment, then legacy config
        var defaultModel = _openAIConfig.Models.FirstOrDefault(m => m.IsDefault);
        if (defaultModel is not null)
            return defaultModel.DeploymentName;

        if (!string.IsNullOrEmpty(_openAIConfig.DefaultDeployment))
            return _openAIConfig.DefaultDeployment;

        return _configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("No Azure OpenAI deployment configured.");
    }

    /// <summary>
    /// Process a chat message through the multi-agent system (non-streaming).
    /// </summary>
    public async Task<Models.ChatResponse> ProcessMessageAsync(
        ChatRequest request,
        string userAccessToken)
    {
        _devOpsService.Initialize(userAccessToken, request.OrganizationUrl);

        var openAIClient = CreateOpenAIClient();
        var deploymentName = ResolveDeploymentName(request.ModelId);

        var searchAgent = _agentFactory.CreateSearchAgent(openAIClient, deploymentName, _devOpsService, request.ProjectName, request.OrganizationUrl);
        var writerAgent = _agentFactory.CreateWriterAgent(openAIClient, deploymentName, _devOpsService);
        var analystAgent = _agentFactory.CreateAnalystAgent(openAIClient, deploymentName, _devOpsService);
        var pipelineAgent = _agentFactory.CreatePipelineAgent(openAIClient, deploymentName, _devOpsService, request.ProjectName, request.OrganizationUrl);
        var wikiAgent = _agentFactory.CreateWikiAgent(openAIClient, deploymentName, _devOpsService, request.ProjectName, request.OrganizationUrl);

        var orchestrator = _agentFactory.CreateOrchestratorAgent(
            openAIClient, deploymentName, searchAgent, writerAgent, analystAgent,
            pipelineAgent, wikiAgent, request.ProjectName, request.OrganizationUrl);

        _logger.LogInformation("Processing message through orchestrator (model: {Deployment}, project: {Project})",
            deploymentName, request.ProjectName ?? "(none)");

        var sw = Stopwatch.StartNew();
        try
        {
            var messages = BuildMessageList(request);
            var session = await orchestrator.CreateSessionAsync();
            var response = await orchestrator.RunAsync(messages, session);

            var reply = response?.ToString() ?? "I wasn't able to process your request. Please try again.";

            // Auto-intercept: if the AI is asking for project despite already having it,
            // re-run with an explicit correction message
            if (!string.IsNullOrEmpty(request.ProjectName) && IsAskingForKnownProject(reply))
            {
                _logger.LogWarning("AI asked for project name despite context. Auto-correcting.");
                var correctionMessages = BuildMessageList(request).ToList();
                correctionMessages.Add(new ChatMessage(ChatRole.Assistant, reply));
                correctionMessages.Add(new ChatMessage(ChatRole.User,
                    $"The project is \"{request.ProjectName}\". You already have this from context. Do NOT ask again. Proceed immediately with the original request."));

                var session2 = await orchestrator.CreateSessionAsync();
                var corrected = await orchestrator.RunAsync(correctionMessages, session2);
                reply = corrected?.ToString() ?? reply;
            }

            // Auto-intercept: if the AI is re-asking for confirmation when the user already said yes,
            // re-run with an explicit "just do it" correction
            if (IsReAskingForConfirmation(reply, request.Message))
            {
                _logger.LogWarning("AI re-asked for confirmation despite user already confirming. Auto-correcting.");
                var correctionMessages = BuildMessageList(request).ToList();
                correctionMessages.Add(new ChatMessage(ChatRole.Assistant, reply));
                correctionMessages.Add(new ChatMessage(ChatRole.User,
                    "I ALREADY CONFIRMED. Do NOT ask me again. Execute the operation IMMEDIATELY right now. " +
                    "Call the appropriate tool and perform the action. No more questions."));

                var session3 = await orchestrator.CreateSessionAsync();
                var corrected = await orchestrator.RunAsync(correctionMessages, session3);
                reply = corrected?.ToString() ?? reply;
            }
            var responseOptions = ExtractSelectableOptions(reply);

            sw.Stop();
            _logger.LogInformation(
                "Chat completed in {DurationMs}ms (model: {ModelId}, project: {Project})",
                sw.ElapsedMilliseconds, request.ModelId ?? "default", request.ProjectName ?? "(none)");

            return new Models.ChatResponse
            {
                Reply = reply,
                SuggestedActions = responseOptions.Count > 0
                    ? responseOptions
                    : _promptService.GetSuggestedActions(reply, request.Message)
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Chat failed after {DurationMs}ms (model: {ModelId}, project: {Project})",
                sw.ElapsedMilliseconds, request.ModelId ?? "default", request.ProjectName ?? "(none)");

            return new Models.ChatResponse
            {
                Reply = $"An error occurred while processing your request: {ex.Message}. " +
                        "Please check your Azure DevOps permissions and try again.",
                SuggestedActions = ["Try rephrasing your request", "Check your connection settings"]
            };
        }
    }

    /// <summary>
    /// Process a message with streaming response that emits structured events
    /// including processing steps (which agent is running, which tool is called).
    /// </summary>
    public async IAsyncEnumerable<StreamEvent> ProcessMessageStreamingAsync(
        ChatRequest request,
        string userAccessToken)
    {
        _devOpsService.Initialize(userAccessToken, request.OrganizationUrl);

        var openAIClient = CreateOpenAIClient();
        var deploymentName = ResolveDeploymentName(request.ModelId);

        // Step 1: Building agents
        yield return new StreamEvent
        {
            Type = "step",
            Step = "thinking",
            Content = "Setting up..."
        };

        var searchAgent = _agentFactory.CreateSearchAgent(openAIClient, deploymentName, _devOpsService, request.ProjectName, request.OrganizationUrl);
        var writerAgent = _agentFactory.CreateWriterAgent(openAIClient, deploymentName, _devOpsService);
        var analystAgent = _agentFactory.CreateAnalystAgent(openAIClient, deploymentName, _devOpsService);
        var pipelineAgent = _agentFactory.CreatePipelineAgent(openAIClient, deploymentName, _devOpsService, request.ProjectName, request.OrganizationUrl);
        var wikiAgent = _agentFactory.CreateWikiAgent(openAIClient, deploymentName, _devOpsService, request.ProjectName, request.OrganizationUrl);

        var orchestrator = _agentFactory.CreateOrchestratorAgent(
            openAIClient, deploymentName, searchAgent, writerAgent, analystAgent,
            pipelineAgent, wikiAgent, request.ProjectName, request.OrganizationUrl);

        // Step 2: Routing
        yield return new StreamEvent
        {
            Type = "step",
            Step = "routing",
            Content = "Analyzing your request..."
        };

        var messages = BuildMessageList(request);

        // Step 3: Run orchestrator with streaming — emit content tokens
        yield return new StreamEvent
        {
            Type = "step",
            Step = "agent",
            Agent = "DevOpsCopilot",
            Content = "Working on your request..."
        };

        var session = await orchestrator.CreateSessionAsync();
        var fullResponse = new System.Text.StringBuilder();
        var emittedFormattingStep = false;

        await foreach (var update in orchestrator.RunStreamingAsync(messages, session))
        {
            var text = update?.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                if (!emittedFormattingStep)
                {
                    yield return new StreamEvent
                    {
                        Type = "step",
                        Step = "formatting",
                        Content = "Preparing your answer..."
                    };
                    emittedFormattingStep = true;
                }

                fullResponse.Append(text);
                yield return new StreamEvent
                {
                    Type = "content",
                    Content = text
                };
            }
        }

        // Auto-intercept: if the AI is asking for project despite already having it,
        // discard the streamed response and re-run with a correction message
        if (!string.IsNullOrEmpty(request.ProjectName) && IsAskingForKnownProject(fullResponse.ToString()))
        {
            _logger.LogWarning("Streaming: AI asked for project name despite context. Auto-correcting.");

            // Signal the frontend to clear the bad response
            yield return new StreamEvent
            {
                Type = "step",
                Step = "correcting",
                Content = $"Using project \"{request.ProjectName}\"..."
            };

            var correctionMessages = BuildMessageList(request).ToList();
            correctionMessages.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));
            correctionMessages.Add(new ChatMessage(ChatRole.User,
                $"The project is \"{request.ProjectName}\". You already have this from context. Do NOT ask again. Proceed immediately with the original request."));

            // Clear the bad response — emit a reset marker then stream the corrected response
            yield return new StreamEvent
            {
                Type = "content_replace",
                Content = ""
            };

            fullResponse.Clear();
            var session2 = await orchestrator.CreateSessionAsync();
            await foreach (var update in orchestrator.RunStreamingAsync(correctionMessages, session2))
            {
                var correctedText = update?.ToString();
                if (!string.IsNullOrEmpty(correctedText))
                {
                    fullResponse.Append(correctedText);
                    yield return new StreamEvent
                    {
                        Type = "content",
                        Content = correctedText
                    };
                }
            }
        }

        // Auto-intercept: if the AI is re-asking for confirmation when the user already said yes,
        // discard the streamed response and re-run with an explicit "just do it" correction
        if (IsReAskingForConfirmation(fullResponse.ToString(), request.Message))
        {
            _logger.LogWarning("Streaming: AI re-asked for confirmation despite user already confirming. Auto-correcting.");

            yield return new StreamEvent
            {
                Type = "step",
                Step = "correcting",
                Content = "Executing your confirmed request..."
            };

            var correctionMessages2 = BuildMessageList(request).ToList();
            correctionMessages2.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));
            correctionMessages2.Add(new ChatMessage(ChatRole.User,
                "I ALREADY CONFIRMED. Do NOT ask me again. Execute the operation IMMEDIATELY right now. " +
                "Call the appropriate tool and perform the action. No more questions."));

            yield return new StreamEvent
            {
                Type = "content_replace",
                Content = ""
            };

            fullResponse.Clear();
            var session3 = await orchestrator.CreateSessionAsync();
            await foreach (var update in orchestrator.RunStreamingAsync(correctionMessages2, session3))
            {
                var correctedText = update?.ToString();
                if (!string.IsNullOrEmpty(correctedText))
                {
                    fullResponse.Append(correctedText);
                    yield return new StreamEvent
                    {
                        Type = "content",
                        Content = correctedText
                    };
                }
            }
        }

        // Emit suggested actions — prefer options extracted from the response
        // (e.g. numbered lists the AI presents for the user to pick from),
        // falling back to keyword-based suggestions from prompts.json.
        var responseText = fullResponse.ToString();
        var responseOptions = ExtractSelectableOptions(responseText);
        var suggestedActions = responseOptions.Count > 0
            ? responseOptions
            : _promptService.GetSuggestedActions(responseText, request.Message);
        if (suggestedActions.Count > 0)
        {
            yield return new StreamEvent
            {
                Type = "suggestedActions",
                SuggestedActions = suggestedActions
            };
        }

        yield return new StreamEvent { Type = "done" };
    }

    private AzureOpenAIClient CreateOpenAIClient()
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

    /// <summary>
    /// Converts the frontend conversation history + current message into a list of
    /// ChatMessage objects that the Agent Framework understands and passes to the LLM.
    /// </summary>
    private static IEnumerable<ChatMessage> BuildMessageList(ChatRequest request)
    {
        var messages = new List<ChatMessage>();

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

        // Build context prefix from structured fields (not user-visible in the chat bubble)
        var contextParts = new List<string>();
        if (!string.IsNullOrEmpty(request.ProjectName))
            contextParts.Add($"[Context: Azure DevOps project = \"{request.ProjectName}\"]");
        if (request.WorkItemContext is { } wi)
            contextParts.Add($"[Context: Work item #{wi.Id} \"{wi.Title}\" ({wi.Type}) — this is the current work item the user is viewing]");

        var prefix = contextParts.Count > 0 ? string.Join("\n", contextParts) + "\n" : "";
        messages.Add(new ChatMessage(ChatRole.User, prefix + request.Message));

        return messages;
    }

    /// <summary>
    /// Extracts selectable options from the AI's response text.
    /// Detects two patterns:
    /// 1. **Actions:** `Option A` · `Option B` — explicit action line format
    /// 2. Numbered lists (1. **Item**) in responses containing a question mark
    /// Returns the option labels so they can be emitted as suggestedActions,
    /// allowing the user to click instead of typing.
    /// </summary>
    private static List<string> ExtractSelectableOptions(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return [];

        // Pattern 1: **Actions:** `Option A` · `Option B` · `Option C`
        var actionsMatch = Regex.Match(responseText, @"\*\*Actions:\*\*\s*(.+)$", RegexOptions.Multiline);
        if (actionsMatch.Success)
        {
            var actionsLine = actionsMatch.Groups[1].Value;
            var actionOptions = Regex.Matches(actionsLine, @"`([^`]+)`")
                .Select(m => m.Groups[1].Value.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length < 100)
                .ToList();
            if (actionOptions.Count >= 2)
                return actionOptions;
        }

        // Pattern 2: Numbered lists in responses with a question
        if (!responseText.Contains('?'))
            return [];

        var matches = Regex.Matches(responseText, @"^\s*\d+\.\s+\*{0,2}(.+?)\*{0,2}\s*$", RegexOptions.Multiline);

        if (matches.Count < 2)
            return [];

        return matches
            .Select(m => m.Groups[1].Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length < 100)
            .ToList();
    }

    /// <summary>
    /// Detects if the AI response is asking the user for a project name
    /// when we already have one from the frontend context. This catches
    /// the failure mode where the AI ignores the [Context: ...] prefix.
    /// </summary>
    private static bool IsAskingForKnownProject(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return false;

        var lower = responseText.ToLowerInvariant();

        // Check for common patterns where the AI asks for project despite having context
        var askingPatterns = new[]
        {
            "which project",
            "what project",
            "project name",
            "provide the azure devops project",
            "tell me the project",
            "confirm the project",
            "specify project",
            "project you want",
            "project context",
            "provide the actual project",
        };

        // Must contain a question and a project-asking pattern
        if (!lower.Contains('?') && !lower.Contains("please provide") && !lower.Contains("please confirm")
            && !lower.Contains("please tell") && !lower.Contains("could you"))
            return false;

        return askingPatterns.Any(pattern => lower.Contains(pattern));
    }

    /// <summary>
    /// Detects if the AI response is re-asking for confirmation when the user
    /// has already confirmed in their latest message. This catches the failure
    /// mode where the AI or a specialist agent asks "please confirm" repeatedly
    /// instead of executing immediately.
    /// </summary>
    private static bool IsReAskingForConfirmation(string responseText, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(responseText) || string.IsNullOrWhiteSpace(userMessage))
            return false;

        // Check if the user's message was a confirmation
        var userLower = userMessage.ToLowerInvariant().Trim();
        var confirmationPhrases = new[]
        {
            "yes", "y", "go ahead", "proceed", "do it", "sure", "ok", "confirm",
            "please", "yep", "yeah", "trigger", "create", "run it", "yes,",
            "yes trigger", "yes, trigger", "yes, go ahead", "yes, create",
            "yes, proceed", "yes, run", "yes please",
        };

        var userConfirmed = confirmationPhrases.Any(phrase => userLower.StartsWith(phrase) || userLower == phrase);
        if (!userConfirmed)
            return false;

        // Check if the AI response is asking for confirmation instead of executing
        var responseLower = responseText.ToLowerInvariant();
        var reConfirmPatterns = new[]
        {
            "please confirm",
            "confirm if you",
            "confirm once more",
            "confirm again",
            "want me to proceed",
            "want me to trigger",
            "want me to create",
            "want me to run",
            "shall i proceed",
            "should i proceed",
            "would you like me to",
            "ready to be run",
            "ready to be triggered",
            "ready to be created",
            "confirm the branch",
            "confirm the path",
            "please confirm if",
        };

        return reConfirmPatterns.Any(pattern => responseLower.Contains(pattern));
    }
}
