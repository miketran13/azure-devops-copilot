# Architecture

## Overview

DevOps Copilot follows a **two-tier architecture**: a frontend Azure DevOps extension communicates with a backend Azure Functions API powered by Microsoft Agent Framework.

```
┌─────────────────────────────────────────────────────┐
│           Azure DevOps (Browser)                    │
│                                                     │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────┐  │
│  │  Hub Page    │  │  WI Form     │  │  Context   │  │
│  │  (Chat UI)   │  │  Group       │  │  Menus     │  │
│  └──────┬───────┘  └──────┬───────┘  └─────┬─────┘  │
│         └─────────────────┼────────────────┘         │
│                           │                          │
│         SDK.getAccessToken() + SDK.getAppToken()     │
└───────────────────────────┼──────────────────────────┘
                            │ HTTPS POST /api/chat
                            │ Authorization: Bearer <user-token>
                            │ X-Extension-Token: <app-jwt>
                            ▼
┌─────────────────────────────────────────────────────┐
│           Azure Functions (.NET 9)                  │
│                                                     │
│  ┌────────────────────────────────────────────────┐ │
│  │  Token Validation Service                      │ │
│  │  • Validates X-Extension-Token JWT             │ │
│  │  • Extracts Bearer token for ADO access        │ │
│  └────────────────────┬───────────────────────────┘ │
│                       │                             │
│  ┌────────────────────▼───────────────────────────┐ │
│  │          Agent Orchestrator                    │ │
│  │                                                │ │
│  │  ┌──────────────┐  ┌─────────────────────────┐│ │
│  │  │ Orchestrator  │──│ Microsoft Agent Framework││ │
│  │  │ Agent         │  │ (AIAgent + Workflows)   ││ │
│  │  └──────┬────────┘  └─────────────────────────┘│ │
│  │         │ .AsAIFunction()                      │ │
│  │  ┌──────┼──────┬──────────┬────────┐           │ │
│  │  ▼      ▼      ▼          ▼        ▼           │ │
│  │ Search Writer Analyst   Pipeline  Wiki         │ │
│  │  Agent  Agent   Agent    Agent   Agent         │ │
│  │  │      │       │         │        │           │ │
│  │  └──────┴───────┴─────────┴────────┘           │ │
│  │            │ Tools (AIFunctionFactory)          │ │
│  │            ▼                                   │ │
│  │  ┌─────────────────────┐                       │ │
│  │  │ AzureDevOpsService  │──► Azure DevOps API   │ │
│  │  └─────────────────────┘   (WIQL, REST 7.1)   │ │
│  └────────────────────────────────────────────────┘ │
│                       │                             │
│                       ▼                             │
│             Azure OpenAI (GPT-4o)                   │
└─────────────────────────────────────────────────────┘
```

## Project Structure

```
devops-copilot/
├── backend/                        # .NET 9 Azure Functions (isolated worker)
│   ├── Agents/
│   │   ├── AgentFactory.cs         # Creates multi-agent system from config
│   │   └── AgentOrchestrator.cs    # Per-request agent lifecycle manager
│   ├── Config/
│   │   ├── prompts.json            # Agent system prompts (hot-reloadable)
│   │   ├── tools.json              # Tool enable/disable, agent assignment
│   │   ├── custom-fields.json      # Custom ADO field mappings
│   │   ├── memory.json             # Session storage settings
│   │   └── mcp.json                # External MCP server connections
│   ├── Functions/
│   │   ├── ChatFunction.cs         # POST /api/chat endpoint
│   │   ├── HealthFunction.cs       # GET /api/health endpoint
│   │   └── SessionFunction.cs      # Session CRUD endpoints
│   ├── Models/                     # Shared DTOs (request, response, work item)
│   ├── Services/
│   │   ├── AzureDevOpsService.cs   # REST client for ADO (WIQL, WI, PR, Branch, Repo)
│   │   ├── TokenValidationService.cs
│   │   ├── PromptConfigurationService.cs
│   │   ├── CustomFieldService.cs
│   │   └── Memory/
│   │       ├── ISessionStore.cs        # Session persistence abstraction
│   │       ├── InMemorySessionStore.cs # Dev/testing (data lost on restart)
│   │       ├── LocalFileSessionStore.cs # File-based persistence
│   │       └── NullSessionStore.cs     # Disabled (no-op)
│   ├── Tools/
│   │   ├── IToolProvider.cs            # Plugin interface for tool groups
│   │   ├── WorkItemSearchToolProvider.cs
│   │   ├── WorkItemWriteToolProvider.cs
│   │   ├── AnalysisToolProvider.cs
│   │   ├── ProjectToolProvider.cs
│   │   ├── PullRequestToolProvider.cs
│   │   ├── BranchToolProvider.cs
│   │   ├── LinkToolProvider.cs
│   │   ├── RepositoryToolProvider.cs
│   │   ├── AttachmentToolProvider.cs
│   │   ├── RelationshipToolProvider.cs
│   │   ├── OrgToolProvider.cs
│   │   ├── PipelineToolProvider.cs
│   │   ├── WikiToolProvider.cs
│   │   └── TestPlanToolProvider.cs
│   ├── Tests/                      # xUnit + Moq tests
│   └── Program.cs                  # DI composition root
├── extension/                      # Azure DevOps Extension (TypeScript + React)
│   ├── src/
│   │   ├── Hub/Hub.tsx             # Main "Copilot" hub page under Boards
│   │   ├── WorkItemGroup/         # AI panel on work item forms
│   │   ├── Actions/               # Context menu action handlers
│   │   ├── components/
│   │   │   ├── ChatPanel.tsx       # Chat interface with FluentUI v9
│   │   │   ├── MessageBubble.tsx   # Markdown-rendered message bubbles
│   │   │   ├── WorkItemCard.tsx    # Work item display card
│   │   │   └── SessionPanel.tsx    # Session history sidebar
│   │   ├── services/backendApi.ts  # HTTP client for backend
│   │   ├── models/types.ts         # TypeScript interfaces
│   │   └── providers/FluentThemeProvider.tsx
│   ├── azure-devops-extension.json # Extension manifest
│   └── webpack.config.js
├── infra/                          # Bicep IaC modules
└── docs/                           # Documentation
```

## Multi-Agent Design

The system uses five specialist agents coordinated by an orchestrator:

| Agent              | Role                                                | Tool Groups                                                                    |
| ------------------ | --------------------------------------------------- | ------------------------------------------------------------------------------ |
| **Orchestrator**   | Routes requests to specialist(s), combines results  | SearchAgent, WriterAgent, AnalystAgent, PipelineAgent, WikiAgent               |
| **Search Agent**   | Finds work items, repositories, and org info        | `workItemSearch`, `project`, `repository`, `org`                               |
| **Writer Agent**   | Creates and updates work items, PRs, and branches   | `workItemWrite`, `pullRequest`, `branch`, `link`, `attachment`, `relationship` |
| **Analyst Agent**  | Analyzes requirements, test plans, and improvements | `analysis`, `testPlan`                                                         |
| **Pipeline Agent** | CI/CD pipelines, builds, releases, variable groups  | `pipeline`                                                                     |
| **Wiki Agent**     | Documentation and wiki page management              | `wiki`                                                                         |

### Why Multi-Agent?

- **Separation of concerns**: Each agent has focused instructions and tools, improving accuracy
- **Safety**: The Writer Agent has explicit instructions to confirm before mutating data
- **Extensibility**: New specialist agents can be added without modifying existing ones
- **Context management**: Each agent's system prompt is smaller and more focused

### Agent Communication Flow

1. User sends a message to `/api/chat`
2. **Orchestrator** receives the message and decides which specialist(s) to invoke
3. Specialists are mounted as tools via `.AsAIFunction()` — the LLM decides when to call them
4. Each specialist uses its own tools to interact with Azure DevOps
5. Results flow back through the orchestrator for a unified response

### Human-in-the-Loop Confirmation

For all write operations (create, update, delete, assign, close):

1. The orchestrator first describes the planned action and asks for confirmation
2. The frontend detects confirmation prompts and shows **Confirm / Edit / Cancel** buttons
3. Only after the user confirms does the orchestrator invoke the Writer Agent
4. This pattern prevents accidental modifications from AI hallucinations

## Authentication & Security

### Token Flow

1. **Extension** calls `SDK.getAccessToken()` → user's Azure DevOps OAuth token
2. **Extension** calls `SDK.getAppToken()` → JWT signed with extension's certificate
3. Both tokens are sent to the backend in HTTP headers
4. **Backend** validates the app token to verify the request comes from the extension
5. **Backend** uses the user's access token to call Azure DevOps APIs on behalf of the user

### Why Token Forwarding?

- The backend acts **on behalf of the user**, respecting their Azure DevOps permissions
- No stored PATs or service accounts needed for normal operation
- The user's identity is preserved in Azure DevOps audit logs

## Known Limitations

### Branch & Code Operations

Git write operations (create branch, push commits) require the `vso.code_write` OAuth scope. While the extension manifest declares this scope, Azure DevOps may not reliably grant it through the extension OAuth token flow. Workarounds:

- **Local development**: Configure a Personal Access Token (PAT) with "Code (Read & Write)" permission in `local.settings.json`
- **Production**: Users may need to uninstall and reinstall the extension after scope changes to re-authorize

Code read operations (file content, directory trees) generally work through the OAuth token.

### Azure DevOps Extension Sandboxing

Azure DevOps extensions run in **sandboxed iframes**. This means:

- No direct DOM access to the host Azure DevOps page
- Communication is only through the official SDK APIs
- Global overlays (floating widgets, modals outside the iframe) are not possible
- Each contribution (hub, form group, action) runs in its own iframe

### AI Model Considerations

- Responses depend on the Azure OpenAI model's capabilities and may not always be accurate
- WIQL query generation can fail for complex or ambiguous requests
- Rate limits and token limits apply to Azure OpenAI API calls
- The Microsoft Agent Framework is currently at **1.0.0-rc4** (pre-release)

## Extension Points

| Contribution         | Type                                   | Placement                            |
| -------------------- | -------------------------------------- | ------------------------------------ |
| Hub                  | `ms.vss-web.hub`                       | New "Copilot" tab under Azure Boards |
| Work Item Form Group | `ms.vss-work-web.work-item-form-group` | AI panel on every work item form     |
| Context Menu Actions | `ms.vss-web.action`                    | Right-click menus on work items      |
| Backlog Menu         | `ms.vss-web.action`                    | Backlog item context menu            |
| Query Toolbar        | `ms.vss-web.action`                    | Query results toolbar                |

## Technology Stack

| Layer            | Technology                                                       |
| ---------------- | ---------------------------------------------------------------- |
| Extension UI     | TypeScript 5.5+, React 18, FluentUI v9, Webpack 5                |
| Extension SDK    | azure-devops-extension-sdk 4.2.0, azure-devops-extension-api 4.x |
| CopilotKit       | @copilotkit/react-core 1.54.0, @copilotkit/react-ui 1.54.0       |
| Backend Runtime  | .NET 9 (isolated worker), Azure Functions v4                     |
| AI Framework     | Microsoft.Extensions.AI.Agents 1.0.0-rc4                         |
| AI Model         | Azure OpenAI GPT-4o-mini (configurable)                          |
| AI Client        | Azure.AI.OpenAI 2.1.0                                            |
| Azure DevOps API | REST API v7.1 (WIQL, Work Items, Git, PRs, Branches)             |
| Infrastructure   | Bicep (modular), Azure Functions Premium EP1                     |
| Monitoring       | Application Insights                                             |
| Secrets          | Azure Key Vault (with Key Vault References)                      |
| Testing          | xUnit 2.x, Moq 4.x                                               |

## Configuration Files

All configuration lives in `backend/Config/` and supports hot-reload:

| File                 | Purpose                                           | Required |
| -------------------- | ------------------------------------------------- | -------- |
| `prompts.json`       | Agent system prompts, greeting, suggested actions | Yes      |
| `tools.json`         | Enable/disable tools, assign to agents            | Yes      |
| `custom-fields.json` | Map process-specific ADO fields                   | No       |
| `memory.json`        | Session storage provider and settings             | No       |
| `mcp.json`           | External MCP server connections                   | No       |

## Session Storage Providers

| Provider    | Config Value | Use Case                         |
| ----------- | ------------ | -------------------------------- |
| None        | (disabled)   | Default — no conversation memory |
| In-Memory   | `inMemory`   | Local development, testing       |
| Local File  | `localFile`  | Single-instance, persistent dev  |
| Azure Table | _(planned)_  | Production multi-instance        |
