# Extending DevOps Copilot

DevOps Copilot is designed to be extended. This guide covers the main extension points.

## Adding a New Agent Tool

Agent tools are C# methods that the AI can call automatically. To add new capabilities:

### 1. Create a Tool Class

```csharp
// backend/Tools/SprintPlanningTools.cs
using System.ComponentModel;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

public sealed class SprintPlanningTools
{
    private readonly AzureDevOpsService _devOps;

    public SprintPlanningTools(AzureDevOpsService devOps)
    {
        _devOps = devOps;
    }

    [Description("Get capacity and workload for a sprint to help with planning")]
    public async Task<string> GetSprintCapacity(
        [Description("Azure DevOps project name")] string project,
        [Description("Iteration path, e.g. 'MyProject\\Sprint 5'")] string iterationPath)
    {
        // Your implementation here
        var items = await _devOps.SearchWorkItemsAsync(
            $"[System.IterationPath] = '{iterationPath}'", project, 100);
        // Analyze and return...
        return "Sprint analysis results...";
    }
}
```

### 2. Register with an Agent

In `backend/Agents/AgentFactory.cs`, add the tools to the appropriate specialist agent:

```csharp
var sprintTools = new SprintPlanningTools(devOpsService);

// Add to the Search Agent or create a new specialist
tools.Add(AIFunctionFactory.Create(sprintTools.GetSprintCapacity));
```

### 3. Add Unit Tests

```csharp
// backend/Tests/SprintPlanningToolsTests.cs
[Fact]
public async Task GetSprintCapacity_ReturnsResults()
{
    // Arrange, Act, Assert
}
```

## Adding a New Specialist Agent

For complex new capabilities, create a dedicated specialist agent:

### 1. Define the Agent

```csharp
// In AgentFactory.cs
public static AIAgent CreatePlanningAgent(
    AzureOpenAIClient client,
    string deploymentName,
    AzureDevOpsService devOpsService)
{
    var sprintTools = new SprintPlanningTools(devOpsService);

    return client.GetChatClient(deploymentName)
        .AsAIAgent(
            name: "PlanningAgent",
            instructions: """
                You are a sprint planning specialist. You help teams plan sprints,
                balance workload, and estimate capacity. Be data-driven and specific.
                """,
            tools: [
                AIFunctionFactory.Create(sprintTools.GetSprintCapacity),
                // Add more planning tools...
            ]);
}
```

### 2. Register with the Orchestrator

```csharp
// In AgentFactory.CreateOrchestratorAgent()
var planningAgent = CreatePlanningAgent(client, deploymentName, devOpsService);

tools.Add(planningAgent.AsAIFunction()); // Mount as a tool for the orchestrator
```

### 3. Update Orchestrator Instructions

Add the new agent's capabilities to the orchestrator's system prompt.

## Adding a New Extension Contribution

### New Hub Tab

1. Create a new folder `extension/src/NewFeature/`
2. Add `NewFeature.html`, `NewFeature.tsx`, `NewFeature.scss`
3. Add a new webpack entry in `webpack.config.js`
4. Add the contribution to `azure-devops-extension.json`
5. Update the `CopyWebpackPlugin` patterns

### New Context Menu Action

1. Add a new handler function in `extension/src/Actions/ContextMenuAction.tsx`
2. Register it with `SDK.register("new-action-id", { execute: handler })`
3. Add the contribution to `azure-devops-extension.json` targeting the desired menu

## Adding a New AI Model Provider

The backend uses Azure OpenAI via `AzureOpenAIClient`. To support additional providers:

1. Update `AgentOrchestrator.CreateOpenAIClient()` to check configuration
2. Add alternative client creation (e.g., `OpenAIClient` for direct OpenAI)
3. The `AIAgent` interface is model-agnostic — the Agent Framework handles the abstraction

## Configuration-Driven Prompts

Agent instructions can be externalized to files for non-developer customization:

```csharp
// Load instructions from a file
var instructions = await File.ReadAllTextAsync("Prompts/SearchAgent.md");

return client.GetChatClient(deploymentName)
    .AsAIAgent(name: "SearchAgent", instructions: instructions, tools: [...]);
```

Store prompt files in `backend/Prompts/` and include them in the build output.
