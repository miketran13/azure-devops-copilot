# How DevOps Copilot Works — End-to-End Architecture

> A deep-dive into how the Microsoft Agent Framework, CopilotKit patterns, Azure Functions, and the Azure DevOps extension frontend fit together to turn a user's natural-language message into real Azure DevOps actions.

---

## Table of Contents

1. [30-Second Overview](#30-second-overview)
2. [Why an Agent Framework?](#why-an-agent-framework)
3. [Why CopilotKit Patterns?](#why-copilotkit-patterns)
4. [The Full Request Lifecycle](#the-full-request-lifecycle)
5. [Backend Architecture — Layer by Layer](#backend-architecture--layer-by-layer)
    - [Program.cs — Dependency Injection & Startup](#programcs--dependency-injection--startup)
    - [Configuration Files](#configuration-files)
    - [Azure Functions (HTTP Endpoints)](#azure-functions-http-endpoints)
    - [AgentOrchestrator — Request Coordinator](#agentorchestrator--request-coordinator)
    - [AgentFactory — Building the Multi-Agent System](#agentfactory--building-the-multi-agent-system)
    - [Tools & IToolProvider — The Hands of the Agent](#tools--itoolprovider--the-hands-of-the-agent)
    - [PromptConfigurationService — Brain Instructions](#promptconfigurationservice--brain-instructions)
    - [AzureDevOpsService — The REST Client](#azuredevopsservice--the-rest-client)
    - [Models — Data Contracts](#models--data-contracts)
    - [Session/Memory Store](#sessionmemory-store)
6. [Frontend Architecture](#frontend-architecture)
    - [Extension SDK & Authentication](#extension-sdk--authentication)
    - [ChatPanel — The Main UI](#chatpanel--the-main-ui)
    - [Backend API Client](#backend-api-client)
    - [Human-in-the-Loop Confirmation](#human-in-the-loop-confirmation)
    - [Session Persistence](#session-persistence)
7. [How a User Prompt Becomes a Tool Call](#how-a-user-prompt-becomes-a-tool-call)
8. [The Multi-Agent "Agent-as-a-Tool" Pattern](#the-multi-agent-agent-as-a-tool-pattern)
9. [Data Flow Diagram](#data-flow-diagram)
10. [Configuration-Driven Design](#configuration-driven-design)
11. [Extensibility — Adding New Capabilities](#extensibility--adding-new-capabilities)

---

## 30-Second Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│  Azure DevOps Extension (Browser)                                   │
│  ChatPanel.tsx → backendApi.ts → POST /api/chat                     │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ HTTP + Bearer Token
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Azure Functions Backend (.NET 9)                                   │
│                                                                     │
│  ChatFunction → AgentOrchestrator → AgentFactory                    │
│                        │                    │                       │
│                        │         ┌──────────┼──────────┐            │
│                        ▼         ▼          ▼          ▼            │
│                   Orchestrator  Search    Writer    Analyst          │
│                     Agent       Agent     Agent     Agent           │
│                        │         │          │          │            │
│                        │         ▼          ▼          ▼            │
│                        │     WorkItem   WorkItem   Analysis        │
│                        │     Search     Write      Tools           │
│                        │     Tools      Tools                      │
│                        │         │          │          │            │
│                        │         └────┬─────┘──────────┘            │
│                        │              ▼                              │
│                        │     AzureDevOpsService                     │
│                        │         (REST API)                         │
│                        │              │                              │
│                        ▼              ▼                              │
│                   Azure OpenAI    Azure DevOps                      │
│                   (GPT-4o-mini)   REST API v7.1                     │
└─────────────────────────────────────────────────────────────────────┘
```

The user types a message in the chat panel. The extension sends it (along with the conversation history and Azure DevOps context) to an Azure Functions backend. The backend feeds the message to a **multi-agent system** built on the Microsoft Agent Framework. The **Orchestrator Agent** reads the message, decides which specialist agent to delegate to, and the specialist calls Azure DevOps REST API tools to fulfill the request. The response flows back to the chat panel.

---

## Why an Agent Framework?

### The Problem Without Agents

A naive approach would be:

1. Take the user's message
2. Send it to a single GPT model with all tools attached
3. Hope the model picks the right tool

This breaks down quickly because:

| Problem                       | Example                                                                                  |
| ----------------------------- | ---------------------------------------------------------------------------------------- |
| **Tool overload**             | With 20+ tools, the LLM gets confused about which to call                                |
| **No separation of concerns** | A search prompt might accidentally trigger a write operation                             |
| **Brittle prompts**           | One giant system prompt trying to cover searching, writing, analyzing becomes unreliable |
| **No workflow coordination**  | Complex tasks like "analyze this story and then create child tasks" need sequencing      |

### What the Agent Framework Provides

The **Microsoft Agent Framework** (`Microsoft.Agents.AI` + `Microsoft.Agents.AI.Workflows`) gives us:

1. **`AIAgent`** — An abstraction that wraps an LLM (Azure OpenAI) with:
    - A dedicated **system prompt** (its "personality" and instructions)
    - A dedicated **toolset** (only the functions it needs)
    - Session management for multi-turn conversation

2. **`AsAIFunction()`** — The "agent-as-a-tool" pattern: any agent can be exposed as a callable tool to another agent. This lets the Orchestrator call specialist agents just like it would call any regular function.

3. **`CreateSessionAsync()` + `RunAsync()`** — The execution loop where the framework automatically:
    - Sends the conversation to the LLM
    - Parses tool-call responses
    - Invokes the requested tools
    - Feeds tool results back to the LLM
    - Repeats until the LLM produces a final text response

Without this framework, you'd need to hand-write the tool-call loop, JSON parsing, error handling, multi-turn state, and agent coordination from scratch.

### The Key Insight: Divide and Conquer

Instead of one overwhelmed agent, we have **four focused agents**:

| Agent            | Role                                    | Tools                                                                                       | System Prompt Focus                                           |
| ---------------- | --------------------------------------- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| **Orchestrator** | Routes requests to the right specialist | SearchAgent, WriterAgent, AnalystAgent (as tools)                                           | Intent classification, confirmation flow, response formatting |
| **SearchAgent**  | Finds work items                        | `SearchWorkItems`, `GetWorkItem`, `GetWorkItemsByIds`, `GetWorkItemTypes`, repository tools | WIQL query generation, result formatting                      |
| **WriterAgent**  | Creates/updates work items              | `CreateWorkItem`, `UpdateWorkItem`, `AddComment`, PR/branch/link tools                      | Content quality, field mapping, immediate execution           |
| **AnalystAgent** | Analyzes and suggests                   | `GetWorkItemForAnalysis`, `GetChildWorkItems`, `GetSprintWorkItems`                         | Requirement quality, test cases, decomposition                |

Each agent is an expert at its slice of the problem. The Orchestrator is the coordinator that knows who to ask.

---

## Why CopilotKit Patterns?

"CopilotKit" here refers to a **design pattern** for building AI copilot experiences, not necessarily the CopilotKit npm library. The patterns we implement are:

### 1. Contextual Awareness

The extension automatically captures the Azure DevOps context (project name, organization URL, user identity) and attaches it to every request. The user never has to say "in project X" — the copilot already knows.

```typescript
// devopsContext.ts — automatic context capture
const context = await getDevOpsContext();
// → { projectName: "MikeDemo", organizationUrl: "https://dev.azure.com/miketran", ... }
```

This context is injected into the user's message:

```csharp
// AgentOrchestrator.cs — context prefix
var currentMessage = $"[Context: Azure DevOps project = \"{request.ProjectName}\"]\n{request.Message}";
```

### 2. Human-in-the-Loop Confirmation

The copilot never performs destructive actions without asking. It describes what it plans to do, shows Confirm/Edit/Cancel buttons, and only proceeds after explicit approval. This is critical for an operations tool that can create/modify production work items.

### 3. Conversation Persistence

Sessions are stored (locally or in cloud storage) so conversations survive page reloads. The user can resume a multi-step workflow later.

### 4. Streaming Responses

The SSE (Server-Sent Events) endpoint provides real-time token streaming so the user sees the response as it's generated, rather than waiting for the full response.

### 5. Suggested Actions

After each response, the copilot suggests relevant follow-up actions based on keyword matching. This guides the user and reduces friction.

### 6. Embedded Experience

The copilot lives inside Azure DevOps as a first-class extension (hub page, context menu actions), not as a separate window. It has access to the current project/work-item context.

### Why Not Just a Plain Chat Box?

Without these patterns, you'd have:

- A generic chatbot that doesn't know your project
- No safety checks before modifying data
- No conversation history across sessions
- No contextual smart suggestions
- An experience bolted on rather than integrated

The CopilotKit patterns turn a generic LLM chat into a **purpose-built operations assistant**.

---

## The Full Request Lifecycle

Here's exactly what happens when a user types "Show me active bugs in Sprint 5":

### Step 1: Frontend — User Input

```
ChatPanel.tsx → handleSend("Show me active bugs in Sprint 5")
```

- Creates a `ChatMessage { role: "user", content: "..." }` and adds it to the UI state
- Auto-creates a session via `POST /api/sessions` if one doesn't exist
- Persists the user message via `POST /api/sessions/{id}/messages`

### Step 2: Frontend — API Call

```
backendApi.ts → chat(message, conversationHistory)
```

- Captures DevOps context: project name, org URL
- Gets OAuth access token from `SDK.getAccessToken()`
- Gets app token from `SDK.getAppToken()`
- Sends `POST /api/chat` with:
    ```json
    {
      "message": "Show me active bugs in Sprint 5",
      "conversationHistory": [...previous messages...],
      "projectName": "MikeDemo",
      "organizationUrl": "https://dev.azure.com/miketran"
    }
    ```
- Headers: `Authorization: Bearer <user-token>`, `X-Extension-Token: <app-token>`

### Step 3: Backend — ChatFunction

```
ChatFunction.cs → Chat(HttpRequest req)
```

1. **Validates app token** — ensures the request comes from the real extension
2. **Extracts bearer token** — the user's Azure DevOps OAuth token
3. **Deserializes request** — parses the JSON body into `ChatRequest`
4. **Delegates to orchestrator** — `_orchestrator.ProcessMessageAsync(chatRequest, userToken)`

### Step 4: Backend — AgentOrchestrator

```
AgentOrchestrator.cs → ProcessMessageAsync(request, userAccessToken)
```

1. **Initializes AzureDevOpsService** — sets the user's OAuth token and org URL on the HTTP client so all subsequent Azure DevOps API calls run with the user's identity/permissions
2. **Creates Azure OpenAI client** — connects to GPT-4o-mini using API key or Managed Identity
3. **Creates specialist agents** via AgentFactory:
    - `CreateSearchAgent(openAIClient, deploymentName, devOpsService)`
    - `CreateWriterAgent(openAIClient, deploymentName, devOpsService)`
    - `CreateAnalystAgent(openAIClient, deploymentName, devOpsService)`
4. **Creates the orchestrator agent** — it receives the three specialists as tools
5. **Builds message list** — converts conversation history + current message into `ChatMessage` objects
6. **Runs the orchestrator** — `orchestrator.CreateSessionAsync()` → `orchestrator.RunAsync(messages, session)`

### Step 5: Backend — AgentFactory Creates Agents

```
AgentFactory.cs → CreateSearchAgent(...) etc.
```

For each specialist:

1. **Loads tools** — scans `IToolProvider` implementations, filters by `tools.json` config:
    ```
    tools.json: "workItemSearch" → agentAssignment: "search" → enabled: true
    ```
    Matching `WorkItemSearchToolProvider.GetTools()` returns `[SearchWorkItems, GetWorkItem, GetWorkItemsByIds]`
2. **Loads system prompt** — reads from `prompts.json` via `PromptConfigurationService`:
    ```
    prompts.json: agents.search.systemPrompt → "You are a specialist Azure DevOps search agent..."
    ```
3. **Creates AIAgent** — wraps the Azure OpenAI chat client with the prompt and tools:
    ```csharp
    openAIClient.GetChatClient(deploymentName)
        .AsIChatClient()
        .AsAIAgent(name: "SearchAgent", instructions: prompt, tools: tools);
    ```

For the orchestrator:

1. The three specialist agents are converted to tools: `searchAgent.AsAIFunction()`, `writerAgent.AsAIFunction()`, `analystAgent.AsAIFunction()`
2. The orchestrator's system prompt (from `prompts.json`) contains routing guidelines

### Step 6: Microsoft Agent Framework — LLM Execution Loop

```
orchestrator.RunAsync(messages, session)
```

This is where the magic happens inside the Agent Framework:

**Iteration 1 — Orchestrator thinks:**

```
System: "You are DevOps Copilot... Routing: 'show me / find / list' → SearchAgent..."
User: "[Context: project = MikeDemo] Show me active bugs in Sprint 5"

LLM Response → Tool Call: SearchAgent("Find active bugs in Sprint 5 for project MikeDemo")
```

**Iteration 2 — SearchAgent executes:**
The framework sees the tool call targets `SearchAgent` (which is itself an AIAgent).
It runs the SearchAgent with the delegated message:

```
System: "You are a specialist Azure DevOps search agent. Convert natural language to WIQL..."
User: "Find active bugs in Sprint 5 for project MikeDemo"

LLM Response → Tool Call: SearchWorkItems(
    whereClause: "[System.WorkItemType] = 'Bug' AND [System.State] = 'Active' AND [System.IterationPath] UNDER 'MikeDemo\\Sprint 5'",
    project: "MikeDemo",
    top: 20
)
```

**Iteration 3 — Tool executes:**
The framework invokes `WorkItemSearchTools.SearchWorkItems(...)`:

- It calls `_devOps.SearchWorkItemsAsync(whereClause, project, top)`
- `AzureDevOpsService` sends a WIQL query to `https://dev.azure.com/miketran/MikeDemo/_apis/wit/wiql`
- Returns JSON results

**Iteration 4 — SearchAgent formats:**
The tool result (JSON list of work items) is fed back to the SearchAgent LLM.
It formats a clean response:

```
Found 3 active bugs in Sprint 5:
1. **#201 — Login timeout on mobile** (Bug, Active, P1) — @Jane
2. **#205 — Export fails for large datasets** (Bug, Active, P2) — @Bob
3. **#212 — UI flicker on dashboard** (Bug, Active, P3) — Unassigned
```

**Iteration 5 — Orchestrator receives and returns:**
The SearchAgent's formatted response becomes the tool result for the Orchestrator.
The Orchestrator passes it through (possibly adding suggested follow-up actions) as the final response.

### Step 7: Backend → Frontend Response

```json
{
    "reply": "Found 3 active bugs in Sprint 5:\n1. **#201 — Login timeout...",
    "suggestedActions": [
        "Show me more bugs",
        "Create a new bug",
        "Analyze bug trends"
    ]
}
```

### Step 8: Frontend — Display

- `ChatPanel` creates an assistant `ChatMessage` and adds it to the message list
- `MessageBubble` renders the markdown response
- Suggested actions appear as clickable chips below the chat
- The assistant message is persisted to the session store

---

## Backend Architecture — Layer by Layer

### Program.cs — Dependency Injection & Startup

The entry point sets up the entire application:

```
Program.cs
├── Load Config Files (prompts.json, tools.json, custom-fields.json, memory.json, mcp.json)
├── Configure CORS
├── Bind Configuration → IOptions<T> (PromptConfiguration, ToolConfiguration, etc.)
├── Register Services (TokenValidation, PromptConfig, CustomField, AzureDevOps)
├── Register Agent Components (AgentFactory, AgentOrchestrator) — Scoped per-request
├── Auto-discover IToolProvider implementations
├── Configure Session Store (NullSessionStore | InMemorySessionStore | LocalFileSessionStore)
└── Build & Run
```

Key design decisions:

- **AgentFactory and AgentOrchestrator are `Scoped`** — a new instance per HTTP request, because each request may have different user credentials
- **AzureDevOpsService is `Scoped`** — initialized per-request with the calling user's OAuth token
- **IToolProvider implementations are `Singleton`** — stateless factories that create tool instances per-request
- **Config files use `reloadOnChange: true`** — prompts and tool settings can be changed without restarting the server

### Configuration Files

| File                        | Purpose                                          | Loaded As                            |
| --------------------------- | ------------------------------------------------ | ------------------------------------ |
| `Config/prompts.json`       | System prompts for each agent, suggested actions | `IOptions<PromptConfiguration>`      |
| `Config/tools.json`         | Enable/disable tools, assign tools to agents     | `IOptions<ToolConfiguration>`        |
| `Config/custom-fields.json` | Map custom Azure DevOps fields to short names    | `IOptions<CustomFieldConfiguration>` |
| `Config/memory.json`        | Session persistence settings                     | `IOptions<MemoryConfiguration>`      |
| `Config/mcp.json`           | MCP server connections (future)                  | `IOptions<McpConfiguration>`         |

This means **you can change agent behavior, enable/disable tools, and modify prompts without changing any code** — just edit JSON files and the server picks up the changes.

### Azure Functions (HTTP Endpoints)

| Function        | Route                              | Purpose                        |
| --------------- | ---------------------------------- | ------------------------------ |
| `Chat`          | `POST /api/chat`                   | Main chat — full JSON response |
| `ChatStream`    | `POST /api/chat/stream`            | Streaming chat via SSE         |
| `Health`        | `GET /api/health`                  | Health check                   |
| `ListSessions`  | `GET /api/sessions`                | List user's saved sessions     |
| `GetSession`    | `GET /api/sessions/{id}`           | Get session with messages      |
| `CreateSession` | `POST /api/sessions`               | Create new session             |
| `DeleteSession` | `DELETE /api/sessions/{id}`        | Delete session                 |
| `UpdateSession` | `PATCH /api/sessions/{id}`         | Rename session                 |
| `AddMessage`    | `POST /api/sessions/{id}/messages` | Append message to session      |

All endpoints require an `Authorization: Bearer <token>` header. The bearer token is the user's Azure DevOps OAuth token, forwarded from the extension. The optional `X-Extension-Token` header carries a JWT for verifying the request comes from the published extension.

### AgentOrchestrator — Request Coordinator

The orchestrator is the central coordination point for each request:

```
ProcessMessageAsync(request, userAccessToken)
│
├── 1. Initialize AzureDevOpsService with user's token
│      → All Azure DevOps API calls now use the user's identity
│
├── 2. Create Azure OpenAI client
│      → API key (dev) or Managed Identity (prod)
│
├── 3. Create 3 specialist agents via AgentFactory
│      → Each gets its own prompt + tools from config
│
├── 4. Create orchestrator agent
│      → Receives specialists as callable tools
│
├── 5. Build message list from conversation history
│      → Prior turns + current message with project context
│
└── 6. Run orchestrator.RunAsync()
       → Agent Framework handles the LLM loop
       → Returns formatted response
```

### AgentFactory — Building the Multi-Agent System

The factory is responsible for **assembling** each agent with the right parts:

```
CreateSearchAgent(openAIClient, deploymentName, devOps)
│
├── GetToolsForAgent("search", devOps)
│   │
│   ├── Scan all IToolProvider implementations
│   ├── For each provider:
│   │   ├── Check tools.json: is this group enabled?
│   │   ├── Check tools.json: is agentAssignment == "search"?
│   │   └── If yes → provider.GetTools(devOps) → AIFunction[]
│   │
│   └── Collected tools: [SearchWorkItems, GetWorkItem, GetWorkItemsByIds,
│                          GetWorkItemTypes, BrowseRepositories, ReadFile, ...]
│
├── GetAgentPrompt("search")
│   └── prompts.json → agents.search.systemPrompt
│
└── openAIClient.AsAIAgent(
        name: "SearchAgent",
        instructions: systemPrompt,
        tools: collectedTools
    )
```

The factory also creates the **Orchestrator Agent** with a crucial difference: its tools are not regular functions but **other agents wrapped as functions**:

```csharp
tools: [
    searchAgent.AsAIFunction(),   // Agent exposed as a callable tool
    writerAgent.AsAIFunction(),   // Agent exposed as a callable tool
    analystAgent.AsAIFunction(),  // Agent exposed as a callable tool
]
```

### Tools & IToolProvider — The Hands of the Agent

Tools are the concrete actions an agent can perform. The architecture has two layers:

**Layer 1: Tool Classes** — Plain C# classes with `[Description]` attributes:

```csharp
public sealed class WorkItemSearchTools
{
    [Description("Search Azure DevOps work items using a WIQL WHERE clause...")]
    public async Task<string> SearchWorkItems(
        [Description("WIQL WHERE clause")] string whereClause,
        [Description("Azure DevOps project name")] string project,
        [Description("Maximum results")] int top = 20)
    {
        var items = await _devOps.SearchWorkItemsAsync(whereClause, project, top);
        return JsonSerializer.Serialize(items);
    }
}
```

The `[Description]` attributes are critical — they become the function descriptions that the LLM reads to understand what each tool does and what parameters it needs.

**Layer 2: Tool Providers** — Implement `IToolProvider` to register tools:

```csharp
public sealed class WorkItemSearchToolProvider : IToolProvider
{
    public string ToolGroupName => "workItemSearch";  // Matches key in tools.json

    public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOpsService)
    {
        var tools = new WorkItemSearchTools(devOpsService);
        return [
            AIFunctionFactory.Create(tools.SearchWorkItems),
            AIFunctionFactory.Create(tools.GetWorkItem),
            AIFunctionFactory.Create(tools.GetWorkItemsByIds),
        ];
    }
}
```

`AIFunctionFactory.Create()` from `Microsoft.Extensions.AI` reads the method signature and `[Description]` attributes to generate a JSON Schema that the LLM uses for function calling.

**Auto-discovery**: `Program.cs` scans the assembly for all `IToolProvider` implementations and registers them:

```csharp
var toolProviderTypes = typeof(IToolProvider).Assembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false }
             && typeof(IToolProvider).IsAssignableFrom(t));
```

This means adding a new tool group is as simple as:

1. Create a tools class with `[Description]`-annotated methods
2. Create an `IToolProvider` implementation
3. Add an entry in `tools.json`

No changes to factory, orchestrator, or startup code needed.

### PromptConfigurationService — Brain Instructions

System prompts define how each agent behaves. They are loaded from `Config/prompts.json` and accessed via `PromptConfigurationService`:

```csharp
public string GetAgentPrompt(string agentName, Dictionary<string, string>? variables = null)
```

Features:

- **Hot-reloadable** via `IOptionsMonitor` — edit the prompt, agent behavior changes immediately
- **Template variables** — `{projectName}`, `{userName}` are substituted at runtime
- **Per-agent isolation** — each agent gets exactly the instructions it needs

The orchestrator's prompt is the most important — it contains:

- Agent routing rules ("Show me..." → SearchAgent)
- Human-in-the-loop confirmation protocol
- Response formatting guidelines (markdown, concise vs. detailed)
- Multi-turn conversation handling rules

### AzureDevOpsService — The REST Client

The service layer that actually talks to Azure DevOps:

```
AzureDevOpsService
├── Initialize(accessToken, organizationUrl)
│   ├── If PAT configured → Basic auth (local dev)
│   └── Else → Bearer token auth (production)
│
├── SearchWorkItemsAsync(whereClause, project, top)
│   └── POST /_apis/wit/wiql → WIQL query → returns work item IDs → batch fetch details
│
├── GetWorkItemAsync(id)
│   └── GET /_apis/wit/workitems/{id}
│
├── CreateWorkItemAsync(project, type, title, ...)
│   └── POST /_apis/wit/workitems/$Bug (with JSON Patch fields)
│
├── UpdateWorkItemAsync(id, fieldUpdates)
│   └── PATCH /_apis/wit/workitems/{id} (with JSON Patch operations)
│
└── ... (branches, PRs, repos, etc.)
```

Critical design: the service uses the **user's own OAuth token** for all API calls. This means:

- The copilot can only access what the user can access
- All changes are made as the user, not a service account
- Azure DevOps audit logs show the real user who made changes

### Models — Data Contracts

| Model                 | Purpose                                                                |
| --------------------- | ---------------------------------------------------------------------- |
| `ChatRequest`         | Frontend → Backend: user message, history, project context, session ID |
| `ChatResponse`        | Backend → Frontend: AI reply, work items, suggested actions            |
| `ConversationMessage` | Single message in history: role + content                              |
| `WorkItemSummary`     | Simplified work item for display (ID, title, type, state, etc.)        |
| Configuration models  | Typed bindings for each JSON config file                               |

### Session/Memory Store

Sessions allow conversation persistence across page reloads:

```
ISessionStore (interface)
├── NullSessionStore      — no-op (when memory disabled)
├── InMemorySessionStore  — Dictionary-based (lost on restart)
└── LocalFileSessionStore — JSON files on disk (survives restart)
    └── Future: AzureTableSessionStore, CosmosDbSessionStore
```

Each session contains:

- Session ID, user ID, project name, title
- Timestamps (created, last active)
- Array of messages (role + content)

---

## Frontend Architecture

### Extension SDK & Authentication

The extension runs inside Azure DevOps as an iframe. It uses the Azure DevOps Extension SDK for:

```typescript
// devopsContext.ts
SDK.getHost()          → Organization name
SDK.getUser()          → Current user info
SDK.getAccessToken()   → OAuth token for Azure DevOps APIs
SDK.getAppToken()      → JWT proving this is the real extension
```

Two tokens are sent with every backend request:

1. **Bearer token** (`Authorization: Bearer <accessToken>`) — The user's Azure DevOps OAuth token. The backend forwards it to Azure DevOps API calls so actions are performed as the user.
2. **App token** (`X-Extension-Token: <appToken>`) — A JWT signed by the extension's certificate. The backend validates it to ensure requests come from the published extension, not a third party.

### ChatPanel — The Main UI

Built with **FluentUI v9** components for a native Azure DevOps look:

```
ChatPanel.tsx
├── Welcome Card (shown when no messages)
│   └── Quick-start buttons: "Summarize my sprint", "What was updated today?", etc.
│
├── Message List
│   └── For each message:
│       ├── MessageBubble (role, content, timestamp)
│       ├── WorkItemCards (if work items returned)
│       └── Action Buttons (if confirmation prompt detected)
│           ├── ✓ Confirm → sends "Yes, go ahead"
│           ├── ✎ Edit → focuses input for modification
│           └── ✕ Cancel → sends "No, cancel that"
│
├── Suggested Actions (chips from last assistant response)
│
├── Error Banner (MessageBar)
│
└── Input Area
    ├── History button → SessionPanel drawer
    ├── Textarea (Enter to send, Shift+Enter for newline)
    └── Send button
```

### Backend API Client

`backendApi.ts` provides two functions:

```typescript
// Synchronous — full response
chat(message, conversationHistory) → Promise<ChatResponse>

// Streaming — SSE chunks
chatStream(message, onChunk, conversationHistory) → Promise<void>
```

Both automatically inject:

- DevOps context (project, org URL)
- Auth tokens (Bearer + app token)
- Conversation history for multi-turn context

### Human-in-the-Loop Confirmation

The confirmation system works across both backend and frontend:

**Backend (Orchestrator prompt)**:

```
For ANY write operation:
1. Describe what you plan to do and ask "Shall I go ahead?"
2. Wait for user's confirmation in the NEXT message
3. When confirmed → IMMEDIATELY call WriterAgent
4. If rejected → adjust and re-confirm
```

**Frontend (ChatPanel)**:

```typescript
// Detect confirmation prompts via regex
const isConfirmationPrompt = (content: string): boolean => {
    const patterns = [
        /shall i (go ahead|proceed|create|update)/i,
        /would you like me to (create|update)/i,
        // ...
    ];
    return patterns.some((p) => p.test(content));
};

// Show Confirm/Edit/Cancel buttons on matching messages
// Track confirmed messages to hide buttons after action
```

This creates a two-phase safety pattern:

1. Agent proposes → buttons appear
2. User confirms → agent executes → buttons disappear

### Session Persistence

The frontend auto-manages sessions:

```
First message → createSession(projectName, title)
Every message → addMessage(sessionId, role, content)
History panel → listSessions() → getSession(id) → restore messages
```

Sessions survive page reloads so users can continue multi-step workflows.

---

## How a User Prompt Becomes a Tool Call

Let's trace the exact transformation of a user's natural language into an API call:

```
User types: "Create a high-priority bug for the login page crash"
       │
       ▼
Frontend adds context: "[Context: project = MikeDemo]\nCreate a high-priority bug for the login page crash"
       │
       ▼
Orchestrator Agent receives messages + system prompt:
  System: "...Routing: 'Create / add / new' → WriterAgent...
           Human-in-the-loop: describe plan and ask confirmation..."
       │
       ▼
LLM decides: This is a create request → needs WriterAgent
             But must confirm first → generate description
       │
       ▼
Orchestrator responds (no tool call yet):
  "I'll create a Bug with these details:
   - **Title**: Login page crash
   - **Priority**: 1 (Critical)
   - **Project**: MikeDemo
   Shall I go ahead and create this?"
       │
       ▼
Frontend detects "Shall I go ahead" → shows [Confirm] [Edit] [Cancel]
       │
User clicks [Confirm] → sends "Yes, go ahead"
       │
       ▼
Orchestrator receives "Yes, go ahead" with full conversation history:
  LLM sees: user asked to create bug → I described it → user confirmed
  LLM decides: Call WriterAgent NOW
       │
       ▼
Tool Call: WriterAgent("Create a high-priority bug titled 'Login page crash' in MikeDemo")
       │
       ▼
WriterAgent receives the delegated message + its system prompt:
  System: "...Execute immediately — do NOT ask for confirmation..."
  LLM decides: Call CreateWorkItem tool
       │
       ▼
Tool Call: CreateWorkItem(
    project: "MikeDemo",
    workItemType: "Bug",
    title: "Login page crash",
    description: "<p>Users are experiencing crashes on the login page...</p>",
    priority: 1
)
       │
       ▼
WorkItemWriteTools.CreateWorkItem() executes:
  → _devOps.CreateWorkItemAsync("MikeDemo", "Bug", "Login page crash", ...)
  → POST https://dev.azure.com/miketran/MikeDemo/_apis/wit/workitems/$Bug
  → Returns: { id: 301, title: "Login page crash", state: "New" }
       │
       ▼
Tool result flows back through: WriterAgent → Orchestrator → ChatFunction → Frontend
       │
       ▼
Final response: "✅ Successfully created Bug #301: 'Login page crash' (P1, New) in MikeDemo"
```

---

## The Multi-Agent "Agent-as-a-Tool" Pattern

This is the core architectural pattern. Here's how it works in code:

```csharp
// AgentFactory.cs — Creating the orchestrator with agents-as-tools

var orchestrator = openAIClient
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsAIAgent(
        name: "DevOpsCopilot",
        instructions: orchestratorPrompt,
        tools: [
            searchAgent.AsAIFunction(),   // ← Agent wrapped as a callable function
            writerAgent.AsAIFunction(),   // ← Agent wrapped as a callable function
            analystAgent.AsAIFunction(),  // ← Agent wrapped as a callable function
        ]);
```

When the orchestrator's LLM produces a tool call like `SearchAgent("find bugs")`:

1. The Agent Framework recognizes `SearchAgent` is itself an `AIAgent`
2. It creates a new inner execution loop for the SearchAgent
3. The SearchAgent's LLM processes the delegated message with its own prompt + tools
4. If the SearchAgent calls tools (e.g., `SearchWorkItems`), those execute synchronously
5. The SearchAgent's final response becomes the tool result for the orchestrator
6. The orchestrator can then format/augment the result before returning to the user

This creates a **two-level execution hierarchy**:

```
Level 0: Orchestrator
    ├── calls → Level 1: SearchAgent
    │               ├── calls → SearchWorkItems (actual function)
    │               └── calls → GetWorkItem (actual function)
    ├── calls → Level 1: WriterAgent
    │               ├── calls → CreateWorkItem (actual function)
    │               └── calls → UpdateWorkItem (actual function)
    └── calls → Level 1: AnalystAgent
                    ├── calls → GetWorkItemForAnalysis (actual function)
                    └── calls → GetChildWorkItems (actual function)
```

---

## Data Flow Diagram

```
                    ┌─────────────────────────────────┐
                    │   Azure DevOps (Browser)         │
                    │                                  │
                    │   SDK.getAccessToken() ──────┐   │
                    │   SDK.getAppToken() ─────────┤   │
                    │   getDevOpsContext() ─────────┤   │
                    │                              │   │
                    │   ChatPanel.tsx               │   │
                    │     │                        │   │
                    │     ▼                        │   │
                    │   backendApi.ts ◄─────────────┘   │
                    │     │                             │
                    └─────┼─────────────────────────────┘
                          │
          POST /api/chat  │  { message, history, project, orgUrl }
          Bearer + AppTkn │
                          ▼
          ┌───────────────────────────────────────────────┐
          │  Azure Functions (.NET 9 Isolated Worker)      │
          │                                                │
          │  ChatFunction.cs                               │
          │    ├── Validate X-Extension-Token               │
          │    ├── Extract Bearer token                     │
          │    └── Call AgentOrchestrator                   │
          │          │                                     │
          │          ▼                                     │
          │  AgentOrchestrator.cs                          │
          │    ├── Initialize AzureDevOpsService(token)     │
          │    ├── Create OpenAI client                     │
          │    ├── AgentFactory.CreateSearchAgent()         │
          │    ├── AgentFactory.CreateWriterAgent()         │
          │    ├── AgentFactory.CreateAnalystAgent()        │
          │    ├── AgentFactory.CreateOrchestratorAgent()   │
          │    ├── BuildMessageList(history + current)      │
          │    └── orchestrator.RunAsync(messages)          │
          │          │                                     │
          │          ▼                                     │
          │  ┌─── Microsoft Agent Framework Loop ───┐      │
          │  │                                      │      │
          │  │  Send messages → Azure OpenAI         │      │
          │  │       │                              │      │
          │  │       ▼                              │      │
          │  │  LLM returns tool_call?              │      │
          │  │    │ YES → Execute tool:             │      │
          │  │    │   ├── SearchAgent.AsAIFunction()│      │
          │  │    │   │     ├── SearchAgent loop    │      │
          │  │    │   │     │   └── SearchWorkItems │──────┼──→ Azure DevOps REST API
          │  │    │   │     │       GetWorkItem     │──────┼──→   (with user's token)
          │  │    │   │     └── returns text        │      │
          │  │    │   ├── WriterAgent.AsAIFunction()│      │
          │  │    │   │     └── CreateWorkItem      │──────┼──→ Azure DevOps REST API
          │  │    │   └── AnalystAgent.AsAIFunction()│     │
          │  │    │         └── GetWorkItemForAnalysis──────┼──→ Azure DevOps REST API
          │  │    │                                 │      │
          │  │    │ Feed tool result back to LLM    │      │
          │  │    └── Loop until text response      │      │
          │  │                                      │      │
          │  │  NO → Return final text response     │      │
          │  └──────────────────────────────────────┘      │
          │                                                │
          │  Return ChatResponse { reply, suggestedActions }│
          └───────────────────────────┬────────────────────┘
                                      │
                          HTTP 200 JSON│
                                      ▼
          ┌───────────────────────────────────────────────┐
          │  ChatPanel.tsx                                 │
          │    ├── Add assistant message to UI             │
          │    ├── Render MessageBubble (markdown)         │
          │    ├── Show WorkItemCards (if any)             │
          │    ├── Show Confirm/Edit/Cancel (if prompt)    │
          │    ├── Show suggested action chips             │
          │    └── Persist to session store                │
          └───────────────────────────────────────────────┘
```

---

## Configuration-Driven Design

The system is designed so that **most behavior changes require zero code changes**:

### Change Agent Behavior → Edit `prompts.json`

```json
"orchestrator": {
    "systemPrompt": "...change routing rules, tone, formatting..."
}
```

### Enable/Disable Tool Groups → Edit `tools.json`

```json
"pullRequest": {
    "enabled": false,  ← Turn off PR tools without touching code
    "agentAssignment": "writer"
}
```

### Reassign Tools Between Agents → Edit `tools.json`

```json
"analysis": {
    "agentAssignment": "search"  ← Move analysis tools from analyst to search agent
}
```

### Add Custom Azure DevOps Fields → Edit `custom-fields.json`

```json
{
    "fieldMappings": [
        {
            "referenceName": "Custom.BusinessValue",
            "shortName": "businessValue",
            "displayName": "Business Value"
        }
    ]
}
```

### Configure Session Storage → Edit `memory.json`

```json
{
    "memory": {
        "enabled": true,
        "provider": "localFile"
    }
}
```

---

## Extensibility — Adding New Capabilities

### Adding a New Tool Group (e.g., Pipeline Triggers)

1. **Create the tools class** — `Tools/PipelineTools.cs`:

    ```csharp
    public sealed class PipelineTools
    {
        [Description("Trigger a pipeline run for a given definition")]
        public async Task<string> TriggerPipeline(
            [Description("Pipeline definition ID")] int definitionId,
            [Description("Branch to run on")] string branch = "main") { ... }
    }
    ```

2. **Create the provider** — `Tools/PipelineToolProvider.cs`:

    ```csharp
    public sealed class PipelineToolProvider : IToolProvider
    {
        public string ToolGroupName => "pipeline";
        public IEnumerable<AIFunction> GetTools(AzureDevOpsService devOps)
        {
            var tools = new PipelineTools(devOps);
            return [ AIFunctionFactory.Create(tools.TriggerPipeline) ];
        }
    }
    ```

3. **Add config** — `Config/tools.json`:

    ```json
    "pipeline": {
        "enabled": true,
        "agentAssignment": "writer",
        "description": "Trigger and manage pipeline runs"
    }
    ```

4. **Done** — no changes to AgentFactory, AgentOrchestrator, Program.cs, or any other file.

### Adding a New Specialist Agent

1. Add agent prompt config in `prompts.json`:

    ```json
    "pipeline": {
        "systemPrompt": "You are a specialist CI/CD agent...",
        "maxTokens": 4096
    }
    ```

2. Add agent config in `tools.json`:

    ```json
    "agents": { "pipeline": { "enabled": true } }
    ```

3. Add factory method in `AgentFactory.cs`: `CreatePipelineAgent(...)`

4. Wire it into the orchestrator's tools in `AgentFactory.CreateOrchestratorAgent()`

5. Update the orchestrator's system prompt in `prompts.json` with new routing rules

---

_This document reflects the architecture as of March 2026. The codebase uses .NET 9, Azure Functions v4 isolated worker model, Microsoft Agent Framework 1.0.0-rc4, and Azure OpenAI with GPT-4o-mini._
