using System.Text.RegularExpressions;
using DevOpsCopilot.Models.Configuration;
using Microsoft.Extensions.Options;

namespace DevOpsCopilot.Services;

/// <summary>
/// Provides access to externalized prompt configurations.
/// Supports hot-reload via IOptionsMonitor and template variable resolution.
/// </summary>
public sealed class PromptConfigurationService
{
    private readonly IOptionsMonitor<PromptConfiguration> _options;

    public PromptConfigurationService(IOptionsMonitor<PromptConfiguration> options)
    {
        _options = options;
    }

    /// <summary>
    /// Get the system prompt for a named agent, with optional template variable resolution.
    /// </summary>
    public string GetAgentPrompt(string agentName, Dictionary<string, string>? variables = null)
    {
        var config = _options.CurrentValue;
        if (!config.Agents.TryGetValue(agentName, out var agentConfig))
        {
            throw new InvalidOperationException(
                $"No prompt configuration found for agent '{agentName}'. " +
                $"Available agents: {string.Join(", ", config.Agents.Keys)}");
        }

        var prompt = agentConfig.SystemPrompt;
        if (variables is { Count: > 0 })
        {
            prompt = ResolveTemplateVariables(prompt, variables);
        }

        return prompt;
    }

    /// <summary>
    /// Get the max tokens setting for an agent.
    /// </summary>
    public int GetMaxTokens(string agentName)
    {
        var config = _options.CurrentValue;
        return config.Agents.TryGetValue(agentName, out var agentConfig)
            ? agentConfig.MaxTokens
            : 4096;
    }

    /// <summary>
    /// Get the default greeting for the orchestrator agent.
    /// </summary>
    public string GetDefaultGreeting()
    {
        var config = _options.CurrentValue;
        return config.Agents.TryGetValue("orchestrator", out var agentConfig)
            ? agentConfig.DefaultGreeting ?? "Hello! How can I help you with Azure DevOps?"
            : "Hello! How can I help you with Azure DevOps?";
    }

    /// <summary>
    /// Generate suggested actions based on keyword matching from the prompt config.
    /// Falls back to defaults if no keyword matches.
    /// </summary>
    public List<string> GetSuggestedActions(string responseText, string userMessage)
    {
        // If the response itself contains a question or error, match on the response content
        // so suggested actions relate to what the AI actually said, not just the user's query.
        var textToMatch = !string.IsNullOrWhiteSpace(responseText)
            ? responseText
            : userMessage;
        var lowerText = textToMatch.ToLowerInvariant();

        var config = _options.CurrentValue;

        foreach (var (pattern, actions) in config.SuggestedActions.Keywords)
        {
            if (Regex.IsMatch(lowerText, pattern, RegexOptions.IgnoreCase))
            {
                return actions;
            }
        }

        // Also try matching on user message if response didn't match
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            var lowerMessage = userMessage.ToLowerInvariant();
            foreach (var (pattern, actions) in config.SuggestedActions.Keywords)
            {
                if (Regex.IsMatch(lowerMessage, pattern, RegexOptions.IgnoreCase))
                {
                    return actions;
                }
            }
        }

        return config.SuggestedActions.Defaults.Count > 0
            ? config.SuggestedActions.Defaults
            : ["Search work items", "Create a work item", "Analyze requirements"];
    }

    /// <summary>
    /// Resolve template variables like {projectName}, {userName}, {organizationUrl}
    /// in prompt text.
    /// </summary>
    private static string ResolveTemplateVariables(string template, Dictionary<string, string> variables)
    {
        foreach (var (key, value) in variables)
        {
            template = template.Replace($"{{{key}}}", value, StringComparison.OrdinalIgnoreCase);
        }
        return template;
    }
}
