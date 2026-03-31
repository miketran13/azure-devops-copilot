using Microsoft.Extensions.AI;

namespace DevOpsCopilot.Services;

/// <summary>
/// Abstraction for creating AI chat clients.
/// Implementations provide clients for different backends (Azure OpenAI, GitHub Models, etc.).
/// </summary>
public interface IChatClientProvider
{
    /// <summary>
    /// Get a chat client configured for the specified model or deployment.
    /// </summary>
    /// <param name="modelOrDeployment">Model ID or deployment name (e.g. "gpt-4o" or "openai/gpt-4o").</param>
    IChatClient GetChatClient(string modelOrDeployment);
}
