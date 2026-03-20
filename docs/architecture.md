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
│  │    ┌────┼──────────────┐                       │ │
│  │    ▼    ▼              ▼                       │ │
│  │ Search  Writer     Analyst                     │ │
│  │ Agent   Agent      Agent                       │ │
│  │    │       │          │                        │ │
│  │    └───────┼──────────┘                        │ │
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

## Multi-Agent Design

The system uses three specialist agents coordinated by an orchestrator:

| Agent             | Role                                               | Tools                                                                     |
| ----------------- | -------------------------------------------------- | ------------------------------------------------------------------------- |
| **Orchestrator**  | Routes requests to specialist(s), combines results | SearchAgent, WriterAgent, AnalystAgent (as tools)                         |
| **Search Agent**  | Finds work items via WIQL queries                  | `SearchWorkItems`, `GetWorkItem`, `GetWorkItemsByIds`, `GetWorkItemTypes` |
| **Writer Agent**  | Creates and updates work items                     | `CreateWorkItem`, `UpdateWorkItem`, `AddComment`                          |
| **Analyst Agent** | Analyzes requirements, suggests improvements       | `GetWorkItemForAnalysis`, `GetChildWorkItems`, `GetSprintWorkItems`       |

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

## Extension Points

| Contribution         | Type                                   | Placement                            |
| -------------------- | -------------------------------------- | ------------------------------------ |
| Hub                  | `ms.vss-web.hub`                       | New "Copilot" tab under Azure Boards |
| Work Item Form Group | `ms.vss-work-web.work-item-form-group` | AI panel on every work item form     |
| Context Menu Actions | `ms.vss-web.action`                    | Right-click menus on work items      |
| Backlog Menu         | `ms.vss-web.action`                    | Backlog item context menu            |
| Query Toolbar        | `ms.vss-web.action`                    | Query results toolbar                |

## Technology Stack

| Layer               | Technology                                                      |
| ------------------- | --------------------------------------------------------------- |
| Extension UI        | TypeScript, React 18, azure-devops-extension-sdk 4.x, Webpack 5 |
| Backend Runtime     | .NET 9 (isolated worker), Azure Functions v4                    |
| AI Framework        | Microsoft Agent Framework 1.0.0-rc4                             |
| AI Model            | Azure OpenAI GPT-4o                                             |
| Azure DevOps Client | Microsoft.TeamFoundationServer.Client 20.x                      |
| Infrastructure      | Bicep (modular), Azure Functions Premium EP1                    |
| CI/CD               | GitHub Actions (OIDC auth)                                      |
| Monitoring          | Application Insights                                            |
| Secrets             | Azure Key Vault (with Key Vault References)                     |
