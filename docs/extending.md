# Extending DevOps Copilot

DevOps Copilot is designed to be extended. The plugin architecture uses auto-discovery — you add new capabilities **without modifying core files** like `AgentFactory.cs` or `Program.cs`.

## Architecture Overview

```
Config/prompts.json       ← Agent system prompts (edit without recompiling)
Config/tools.json         ← Enable/disable tools and assign to agents
Config/custom-fields.json ← Custom ADO field mappings
Config/memory.json        ← Session storage settings
Config/mcp.json           ← External MCP server connections

Tools/IToolProvider.cs    ← Interface for pluggable tool groups
Services/                 ← Core services (AzureDevOps, Prompts, Memory)
Agents/AgentFactory.cs    ← Reads config, auto-discovers tools, builds agents
```

## Adding a New Tool (Quickstart)

The fastest way to add new capabilities. **No core files need to change.**

### 1. Create the Tool Class

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
        var items = await _devOps.SearchWorkItemsAsync(
            $"[System.IterationPath] = '{iterationPath}'", project, 100);
        return "Sprint analysis results...";
    }
}
```

### 2. Create the Tool Provider

```csharp
// backend/Tools/SprintPlanningToolProvider.cs
using Microsoft.Extensions.AI;
using DevOpsCopilot.Services;

namespace DevOpsCopilot.Tools;

public sealed class SprintPlanningToolProvider : IToolProvider
{
    public string ToolGroupName => "sprintPlanning"; // Must match tools.json key

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new SprintPlanningTools(devOpsService);
        return [AIFunctionFactory.Create(tools.GetSprintCapacity)];
    }
}
```

### 3. Register in Config

Add an entry to `Config/tools.json`:

```json
{
    "tools": {
        "sprintPlanning": {
            "enabled": true,
            "agentAssignment": "analyst",
            "description": "Sprint capacity and workload planning"
        }
    }
}
```

### 4. Build and Run

```bash
dotnet build
func start
```

The tool is auto-discovered and registered with the analyst agent. No changes to `AgentFactory.cs`, `Program.cs`, or any other file.

> **Tip**: Copy `backend/Tools/_ToolTemplate.cs` as a starting point.

## Editing Agent Prompts (No Code Changes)

All agent prompts live in `Config/prompts.json`. Edit the file and restart (or wait for hot-reload) to change agent behavior.

```json
{
    "agents": {
        "orchestrator": {
            "systemPrompt": "You are DevOps Copilot...",
            "defaultGreeting": "Hello!...",
            "maxTokens": 4096
        },
        "search": { "systemPrompt": "..." },
        "writer": { "systemPrompt": "..." },
        "analyst": { "systemPrompt": "..." }
    },
    "suggestedActions": {
        "keywords": {
            "bug|issue": ["Show me more bugs", "Create a new bug"]
        },
        "defaults": ["Search work items", "Create a work item"]
    }
}
```

Prompts support template variables: `{projectName}`, `{userName}`, `{organizationUrl}`.

## Adding Custom Fields

Map process-specific ADO fields in `Config/custom-fields.json`:

```json
{
    "fieldMappings": [
        {
            "referenceName": "Custom.BusinessValue",
            "displayName": "Business Value",
            "shortName": "businessValue",
            "type": "integer",
            "workItemTypes": ["User Story", "Feature"],
            "includeInSearch": true,
            "includeInDisplay": true
        }
    ]
}
```

Custom fields appear in `WorkItemSummary.CustomFields` dictionary and are automatically included when fetching work items.

## Adding a New Agent

For complex new capabilities that need their own specialist:

### 1. Add Prompt to Config

Add the new agent to `Config/prompts.json`:

```json
{
    "agents": {
        "planning": {
            "systemPrompt": "You are a sprint planning specialist...",
            "maxTokens": 4096
        }
    }
}
```

### 2. Add Agent Toggle to tools.json

```json
{
    "agents": {
        "planning": { "enabled": true }
    }
}
```

### 3. Create Tool Provider

Assign tools to the new agent name in `Config/tools.json`:

```json
{
    "tools": {
        "sprintPlanning": {
            "enabled": true,
            "agentAssignment": "planning"
        }
    }
}
```

### 4. Register Agent in AgentFactory

Add a `CreatePlanningAgent()` method in `AgentFactory.cs` and mount it on the orchestrator:

```csharp
public AIAgent CreatePlanningAgent(
    AzureOpenAIClient openAIClient, string deploymentName, AzureDevOpsService devOps)
{
    var tools = GetToolsForAgent("planning", devOps);
    var prompt = _promptService.GetAgentPrompt("planning");
    return openAIClient.GetChatClient(deploymentName).AsIChatClient()
        .AsAIAgent(name: "PlanningAgent", instructions: prompt,
                   tools: tools.Cast<AITool>().ToList());
}
```

## Connecting an MCP Server

Add external tool servers via `Config/mcp.json`:

```json
{
    "mcpServers": [
        {
            "name": "my-tools",
            "endpoint": "http://localhost:8080",
            "transport": "sse",
            "enabled": true,
            "authentication": {
                "type": "bearer",
                "tokenSource": "env:MCP_TOKEN"
            },
            "agentAssignments": ["search"]
        }
    ]
}
```

## Creating a Custom Session Store

Implement `ISessionStore` and register it in `Program.cs`:

```csharp
public sealed class RedisSessionStore : ISessionStore
{
    public Task<SessionInfo> CreateSessionAsync(string userId, string? projectName, string title) { ... }
    public Task<SessionInfo?> GetSessionAsync(string sessionId, string userId) { ... }
    public Task<List<SessionInfo>> ListSessionsAsync(string userId, string? projectName, int skip, int take) { ... }
    public Task AddMessageAsync(string sessionId, string role, string content) { ... }
    public Task DeleteSessionAsync(string sessionId, string userId) { ... }
    public Task UpdateSessionTitleAsync(string sessionId, string userId, string title) { ... }
}
```

Built-in stores: `InMemorySessionStore` (dev), `NullSessionStore` (disabled), with `AzureTableSessionStore` planned.

## Adding a New Extension Contribution

### New Hub Tab

1. Create a new folder `extension/src/NewFeature/`
2. Add `NewFeature.html`, `NewFeature.tsx`, `NewFeature.scss`
3. Add a new webpack entry in `webpack.config.js`
4. Add the contribution to `azure-devops-extension.json`

### New Context Menu Action

1. Add a new handler function in `extension/src/Actions/ContextMenuAction.tsx`
2. Register it with `SDK.register("new-action-id", { execute: handler })`
3. Add the contribution to `azure-devops-extension.json`

## Adding a New AI Model Provider

The backend uses Azure OpenAI via `AzureOpenAIClient`. To support additional providers:

1. Update `AgentOrchestrator.CreateOpenAIClient()` to check configuration
2. Add alternative client creation (e.g., `OpenAIClient` for direct OpenAI)
3. The `AIAgent` interface is model-agnostic — the Agent Framework handles the abstraction
