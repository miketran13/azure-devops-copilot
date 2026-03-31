using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using DevOpsCopilot.Models.Configuration;
using DevOpsCopilot.Services;
using DevOpsCopilot.Tools;

namespace DevOpsCopilot.Agents;

/// <summary>
/// Factory that creates and configures the multi-agent system.
/// Each specialist agent has a focused role and toolset; the orchestrator
/// coordinates them using the agent-as-a-tool pattern.
///
/// Prompts are loaded from Config/prompts.json via PromptConfigurationService.
/// Tools are loaded from IToolProvider implementations and routed by Config/tools.json.
/// </summary>
public sealed class AgentFactory
{
    private readonly PromptConfigurationService _promptService;
    private readonly IEnumerable<IToolProvider> _toolProviders;
    private readonly ToolConfiguration _toolConfig;
    private readonly ILogger<AgentFactory> _logger;

    public AgentFactory(
        PromptConfigurationService promptService,
        IEnumerable<IToolProvider> toolProviders,
        Microsoft.Extensions.Options.IOptions<ToolConfiguration> toolConfig,
        ILogger<AgentFactory> logger)
    {
        _promptService = promptService;
        _toolProviders = toolProviders;
        _toolConfig = toolConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Collect all enabled tools assigned to a given agent name.
    /// </summary>
    private List<AIFunction> GetToolsForAgent(string agentName, AzureDevOpsService devOps)
    {
        var functions = new List<AIFunction>();
        foreach (var provider in _toolProviders)
        {
            // Check if this tool group is enabled and assigned to this agent
            if (_toolConfig.Tools.TryGetValue(provider.ToolGroupName, out var groupCfg)
                && groupCfg.Enabled
                && string.Equals(groupCfg.AgentAssignment, agentName, StringComparison.OrdinalIgnoreCase))
            {
                var tools = provider.GetTools(devOps).ToList();
                _logger.LogDebug("Loaded {Count} tools from '{Group}' for agent '{Agent}'",
                    tools.Count, provider.ToolGroupName, agentName);
                functions.AddRange(tools);
            }
        }
        return functions;
    }

    /// <summary>
    /// Creates the SearchAgent — specialist for finding and querying work items.
    /// </summary>
    public AIAgent CreateSearchAgent(
        IChatClient chatClient, AzureDevOpsService devOps,
        string? projectName = null, string? organizationUrl = null)
    {
        var tools = GetToolsForAgent("search", devOps);
        var variables = BuildProjectVariables(projectName, organizationUrl);
        var prompt = _promptService.GetAgentPrompt("search", variables);

        _logger.LogInformation("Creating SearchAgent with {ToolCount} tools", tools.Count);

        return chatClient
            .AsAIAgent(
                name: "SearchAgent",
                instructions: prompt,
                tools: tools.Cast<AITool>().ToList());
    }

    /// <summary>
    /// Creates the WriterAgent — specialist for creating and updating work items.
    /// </summary>
    public AIAgent CreateWriterAgent(
        IChatClient chatClient, AzureDevOpsService devOps)
    {
        var tools = GetToolsForAgent("writer", devOps);
        var prompt = _promptService.GetAgentPrompt("writer");

        _logger.LogInformation("Creating WriterAgent with {ToolCount} tools", tools.Count);

        return chatClient
            .AsAIAgent(
                name: "WriterAgent",
                instructions: prompt,
                tools: tools.Cast<AITool>().ToList());
    }

    /// <summary>
    /// Creates the AnalystAgent — specialist for analyzing requirements, generating
    /// test cases, and suggesting improvements.
    /// </summary>
    public AIAgent CreateAnalystAgent(
        IChatClient chatClient, AzureDevOpsService devOps)
    {
        var tools = GetToolsForAgent("analyst", devOps);
        var prompt = _promptService.GetAgentPrompt("analyst");

        _logger.LogInformation("Creating AnalystAgent with {ToolCount} tools", tools.Count);

        return chatClient
            .AsAIAgent(
                name: "AnalystAgent",
                instructions: prompt,
                tools: tools.Cast<AITool>().ToList());
    }

    /// <summary>
    /// Creates the PipelineAgent — specialist for CI/CD pipeline, build, release,
    /// and variable group operations.
    /// </summary>
    public AIAgent CreatePipelineAgent(
        IChatClient chatClient, AzureDevOpsService devOps,
        string? projectName = null, string? organizationUrl = null)
    {
        var tools = GetToolsForAgent("pipeline", devOps);
        var variables = BuildProjectVariables(projectName, organizationUrl);
        var prompt = _promptService.GetAgentPrompt("pipeline", variables);

        _logger.LogInformation("Creating PipelineAgent with {ToolCount} tools", tools.Count);

        return chatClient
            .AsAIAgent(
                name: "PipelineAgent",
                instructions: prompt,
                tools: tools.Cast<AITool>().ToList());
    }

    /// <summary>
    /// Creates the WikiAgent — specialist for documentation and wiki page management.
    /// </summary>
    public AIAgent CreateWikiAgent(
        IChatClient chatClient, AzureDevOpsService devOps,
        string? projectName = null, string? organizationUrl = null)
    {
        var tools = GetToolsForAgent("wiki", devOps);
        var variables = BuildProjectVariables(projectName, organizationUrl);
        var prompt = _promptService.GetAgentPrompt("wiki", variables);

        _logger.LogInformation("Creating WikiAgent with {ToolCount} tools", tools.Count);

        return chatClient
            .AsAIAgent(
                name: "WikiAgent",
                instructions: prompt,
                tools: tools.Cast<AITool>().ToList());
    }

    /// <summary>
    /// Creates the OrchestratorAgent — coordinates specialist agents based on user intent.
    /// </summary>
    public AIAgent CreateOrchestratorAgent(
        IChatClient chatClient,
        AIAgent searchAgent, AIAgent writerAgent, AIAgent analystAgent,
        AIAgent pipelineAgent, AIAgent wikiAgent,
        string? projectName = null, string? organizationUrl = null)
    {
        var variables = BuildProjectVariables(projectName, organizationUrl);
        var prompt = _promptService.GetAgentPrompt("orchestrator", variables);

        _logger.LogInformation("Creating OrchestratorAgent (project: {Project})", projectName ?? "none");

        return chatClient
            .AsAIAgent(
                name: "DevOpsCopilot",
                instructions: prompt,
                tools: [
                    searchAgent.AsAIFunction(),
                    writerAgent.AsAIFunction(),
                    analystAgent.AsAIFunction(),
                    pipelineAgent.AsAIFunction(),
                    wikiAgent.AsAIFunction(),
                ]);
    }

    private static Dictionary<string, string> BuildProjectVariables(string? projectName, string? organizationUrl = null)
    {
        var vars = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(projectName))
        {
            vars["projectName"] = projectName;
            vars["projectContext"] = $"The user is working in Azure DevOps project \"{projectName}\". Use this project for ALL operations. Do NOT ask for the project name.";
        }
        else
        {
            vars["projectName"] = "";
            vars["projectContext"] = "No project context was provided. If you need a project name, call SearchAgent with ListProjects to get real project names. NEVER invent project names.";
        }
        vars["organizationUrl"] = organizationUrl?.TrimEnd('/') ?? "";
        return vars;
    }
}
