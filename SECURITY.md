# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in this project, please report it responsibly.

**Please do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please send an email to the maintainers describing:

1. The type of vulnerability
2. Steps to reproduce
3. Potential impact
4. Suggested fix (if any)

We will acknowledge receipt within 48 hours and provide a detailed response within 7 days.

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |

## Architecture & Security Model

### How the Azure Function Is Protected

The Azure Functions backend uses **application-level authentication** rather than function keys. All HTTP functions are set to `AuthorizationLevel.Anonymous` at the Azure Functions runtime level — authentication is enforced in application code using a **two-token scheme**.

#### Two-Token Authentication

Every protected API call from the extension sends two tokens:

| Header                          | Token            | Source                 | Purpose                                                            |
| ------------------------------- | ---------------- | ---------------------- | ------------------------------------------------------------------ |
| `X-Extension-Token`             | App JWT          | `SDK.getAppToken()`    | Proves the request comes from the published Azure DevOps extension |
| `Authorization: Bearer <token>` | User OAuth token | `SDK.getAccessToken()` | The user's Azure DevOps identity, forwarded to DevOps APIs         |

**How it works:**

1. The extension calls `SDK.getAppToken()` — Azure DevOps signs a JWT using the extension's **shared secret** (stored in Key Vault as `extension-shared-secret`)
2. The extension calls `SDK.getAccessToken()` — Azure DevOps returns the user's OAuth bearer token
3. Both tokens are sent to the backend on every request
4. The backend's `TokenValidationService` validates the app JWT signature against the same shared secret
5. The user's OAuth token is forwarded to Azure DevOps APIs — permissions are enforced by Azure DevOps, not the backend

**This means an anonymous attacker cannot call the API** — they would need:

- A valid app token (requires the extension's shared secret from Key Vault)
- A valid user OAuth token (requires being an authenticated Azure DevOps user with the extension installed)

#### Endpoint Protection Matrix

| Endpoint                                | App Token    | User Token   | Reason                                              |
| --------------------------------------- | ------------ | ------------ | --------------------------------------------------- |
| `POST /api/chat`                        | Required     | Required     | AI chat with DevOps access                          |
| `POST /api/chat/stream`                 | Required     | Required     | Streaming AI chat                                   |
| `GET/POST/PATCH/DELETE /api/sessions/*` | Required     | Required     | Session management (user-scoped)                    |
| `GET /api/health`                       | Not required | Not required | Health monitoring probe — returns no sensitive data |
| `GET /api/models`                       | Not required | Not required | Lists available AI model names — no sensitive data  |

#### Session Isolation

Session endpoints derive a user ID from a SHA-256 hash of the user's OAuth token (first 16 base64 characters). This means:

- Each user can only access their own sessions
- No PII (names, emails) is stored — only a one-way hash
- Sessions cannot be accessed by other users, even with a valid app token

#### Development Mode

When `Extension:SharedSecret` is not configured (empty or null), app token validation is **skipped** with a warning log. This allows local development without Key Vault access. In production, the secret must be set in Key Vault.

### Transport & Platform Security

| Control        | Setting                       | Location                                     |
| -------------- | ----------------------------- | -------------------------------------------- |
| HTTPS only     | `httpsOnly: true`             | Bicep (`function-app.bicep`)                 |
| Minimum TLS    | 1.2                           | Bicep (`minTlsVersion: '1.2'`)               |
| FTP disabled   | `ftpsState: 'Disabled'`       | Bicep                                        |
| FTP basic auth | Disabled                      | Bicep (`basicPublishingCredentialsPolicies`) |
| CORS           | Allowlist of specific origins | Bicep, `host.json`, `Program.cs`             |

### Key Vault Integration

Production secrets are stored in Azure Key Vault and referenced via `@Microsoft.KeyVault()` syntax:

| App Setting               | Key Vault Secret          | Purpose                                  |
| ------------------------- | ------------------------- | ---------------------------------------- |
| `AzureOpenAI__ApiKey`     | `openai-api-key`          | Azure OpenAI API key                     |
| `Extension__SharedSecret` | `extension-shared-secret` | JWT signing key for app token validation |

Access is granted via RBAC — the Function App's **system-assigned managed identity** receives the **Key Vault Secrets User** role. No access policies or shared connection strings are used.

### Authentication Flow

DevOps Copilot uses a **token-forwarding** architecture — the backend never stores credentials:

1. The Azure DevOps extension calls `SDK.getAccessToken()` to get the user's OAuth bearer token
2. The extension calls `SDK.getAppToken()` to get a signed JWT proving the request comes from the extension
3. Both tokens are sent to the Azure Functions backend in HTTP headers
4. The backend validates the app JWT, then uses the user's OAuth token to call Azure DevOps APIs
5. All operations are scoped to the user's own Azure DevOps permissions

This means:

- **No service accounts** or stored PATs in production
- **User identity** is preserved in Azure DevOps audit logs
- **Permissions** are enforced by Azure DevOps — the backend cannot escalate access

### Data Flow

- Messages are sent to Azure OpenAI for LLM processing
- Work item data is retrieved from Azure DevOps APIs in real-time
- No work item data is persisted by the backend (unless session memory is explicitly enabled)
- Session data (when enabled) is stored in the configured session store (in-memory, local file, or Azure Table Storage)

## Pre-Release Dependencies Warning

> **Important:** This project uses **Microsoft.Extensions.AI.Agents 1.0.0-rc4** (release candidate).

Release candidate packages:

- May contain unforeseen bugs, security vulnerabilities, or behavioral differences
- Can introduce breaking API changes between RC versions
- Have not received the same level of security review as GA (Generally Available) releases
- Should be evaluated carefully before production deployment

**Recommendation:** Monitor the [Microsoft.Extensions.AI](https://github.com/dotnet/extensions) repository for GA releases and upgrade promptly.

## Security Best Practices for Deployment

- **Never** commit `local.settings.json` or `.env` files
- Use **Azure Key Vault** for all secrets in production
- Use **Managed Identity** for Azure resource access
- Enable **HTTPS only** on Azure Functions
- Rotate API keys and PATs regularly
- Review Azure DevOps OAuth scopes — grant minimum required permissions
- The extension requests these scopes: `vso.work_write`, `vso.project`, `vso.code_write`
- For local development, a Personal Access Token (PAT) may be used — store it only in `local.settings.json` (git-ignored)
- Review the [Azure OpenAI content filtering](https://learn.microsoft.com/azure/ai-services/openai/concepts/content-filter) settings for your deployment

## AI-Specific Risks

- **Prompt injection**: Malicious work item content could attempt to manipulate AI behavior. The multi-agent architecture with separate system prompts provides some isolation.
- **Data exposure**: The AI model receives work item content — ensure your Azure OpenAI resource has appropriate data handling settings.
- **Hallucination**: AI responses may contain inaccurate information. The human-in-the-loop confirmation pattern for write operations mitigates the risk of incorrect modifications.
- **Cost**: Each chat interaction incurs Azure OpenAI API costs. Consider setting up usage limits and monitoring.
