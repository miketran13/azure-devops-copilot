# Getting Started

This guide walks you through setting up a local development environment for the DevOps Copilot extension.

There are two modes:

| Mode                             | What you need                        |
| -------------------------------- | ------------------------------------ |
| **Azure OpenAI + ADO Extension** | Azure subscription, Azure DevOps org |
| **GitHub Models + Standalone**   | GitHub account only (free)           |

## Prerequisites

| Tool                       | Version | Install                                                                          |
| -------------------------- | ------- | -------------------------------------------------------------------------------- |
| .NET SDK                   | 9.0+    | [Download](https://dotnet.microsoft.com/download/dotnet/9.0)                     |
| Node.js                    | 20+     | [Download](https://nodejs.org/)                                                  |
| Azure Functions Core Tools | v4      | [Install](https://learn.microsoft.com/azure/azure-functions/functions-run-local) |
| tfx-cli                    | latest  | `npm install -g tfx-cli` (only for ADO extension mode)                           |

Additionally:

- **Azure OpenAI + Extension mode:** An [Azure OpenAI resource](https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAI) with a GPT-4o deployment + an [Azure DevOps org](https://dev.azure.com)
- **GitHub Models + Standalone mode:** A [GitHub Personal Access Token](https://github.com/settings/tokens) with **`models:read`** scope — nothing else required

> **Tip:** Use the included `.devcontainer/` for GitHub Codespaces or VS Code Dev Containers — all tools are pre-installed.

## 1. Clone the Repository

```bash
git clone https://github.com/YOUR-ORG/devops-copilot.git
cd devops-copilot
```

## 2. Backend Setup

```bash
cd backend

# Create your local settings from the template
cp local.settings.example.json local.settings.json
```

### Option A — Azure OpenAI (full ADO extension experience)

Edit `local.settings.json`:

```json
{
    "Values": {
        "AIProvider": "AzureOpenAI",
        "AppMode": "AzureDevOps",
        "AzureOpenAI__Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
        "AzureOpenAI__DefaultDeployment": "gpt-4o",
        "AzureOpenAI__ApiKey": "YOUR-KEY",
        "AzureDevOps__DefaultOrganizationUrl": "https://dev.azure.com/YOUR-ORG",
        "Memory__Provider": "localFile",
        "Memory__LocalFilePath": "./sessions"
    },
    "Host": {
        "CORS": "https://localhost:3000,https://dev.azure.com,https://*.visualstudio.com",
        "CORSCredentials": true
    }
}
```

### Option B — GitHub Models + Standalone (no Azure required)

This runs entirely without an Azure subscription. Users authenticate with a GitHub PAT directly in the browser.

**First:** Create a GitHub PAT at [github.com/settings/tokens](https://github.com/settings/tokens) with the **`models:read`** permission (under _GitHub Models_).

Then edit `local.settings.json` with this minimal config:

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "AIProvider": "GitHubModels",
        "AppMode": "Standalone",
        "GitHubModels__Endpoint": "https://models.github.ai/inference/",
        "GitHubModels__ApiKey": "",
        "GitHubModels__DefaultModel": "openai/gpt-4o-mini",
        "Memory__Provider": "localFile",
        "Memory__LocalFilePath": "./sessions",
        "Cors__AllowedOrigins": "https://localhost:3000,http://localhost:3000"
    },
    "Host": {
        "CORS": "https://localhost:3000,http://localhost:3000",
        "CORSCredentials": true
    }
}
```

**Key settings:**

| Setting                      | Value                  | Notes                                                                              |
| ---------------------------- | ---------------------- | ---------------------------------------------------------------------------------- |
| `AIProvider`                 | `"GitHubModels"`       | Switches AI backend from Azure OpenAI to GitHub Models                             |
| `AppMode`                    | `"Standalone"`         | Disables Azure DevOps extension token checks                                       |
| `GitHubModels__ApiKey`       | _(empty or your PAT)_  | Leave blank — users enter their own PAT in the UI. Or set a shared server-side key |
| `GitHubModels__DefaultModel` | `"openai/gpt-4o-mini"` | Use `openai/gpt-4o` for more capable responses (lower rate limits)                 |

> **Rate limits:** GitHub Models free tier is ~10 RPM / 50 RPD for GPT-4o. Each chat message triggers 3–6 internal LLM calls through multi-agent orchestration — expect ~8–15 usable interactions/day on free tier. `gpt-4o-mini` has higher limits.

Run the backend:

```bash
dotnet restore
dotnet build
func start
```

The API will be available at `http://localhost:7071/api`. Test with:

```bash
curl http://localhost:7071/api/health
```

## 3. Extension / Frontend Setup

### Option A — Standalone web app (GitHub Models mode)

No extension install required. Just start the dev server:

```bash
cd extension
npm install
npm run dev
```

Then open **`https://localhost:3000/standalone/standalone.html`** in your browser:

1. Accept the self-signed certificate warning (**Advanced → Proceed to localhost**)
2. Click the **gear icon** (⚙) in the top-right corner to open Settings
3. Paste your GitHub PAT and optionally your ADO org URL + ADO PAT for work item access
4. Click **Save** and start chatting

> If you set `GitHubModels__ApiKey` in `local.settings.json`, skip the PAT step — the backend uses it automatically.

### Option B — Azure DevOps Extension (ADO mode)

```bash
cd extension
npm install
npm run build
```

### Sideloading for Development

> **Note:** Azure DevOps **Services** (cloud, dev.azure.com) does **not** have a "Browse local extensions" upload button — that option only exists on self-hosted Azure DevOps Server. For cloud-hosted Azure DevOps you must publish via the Visual Studio Marketplace (even for private dev extensions).

**One-time setup:** Create a free publisher account at https://marketplace.visualstudio.com/manage and update the `publisher` field in `extension/azure-devops-extension.json`.

1. Package with dev overrides (backend URL pointed to `localhost:7071`):

    ```bash
    npm run package:dev
    ```

2. Publish the `.vsix` as a **private** extension:

    ```powershell
    # PowerShell (Windows) — (Get-Item ...) resolves the glob since PowerShell doesn't expand *.vsix automatically
    npx tfx-cli extension publish `
      --vsix (Get-Item dist/*.vsix).FullName `
      --token YOUR-MARKETPLACE-PAT
    ```

    ```bash
    # bash / macOS / Linux
    npx tfx-cli extension publish \
      --vsix dist/*.vsix \
      --token YOUR-MARKETPLACE-PAT
    ```

    Generate a PAT at https://dev.azure.com → User Settings → Personal Access Tokens with scope **Marketplace → Manage**.

3. Share it with your Azure DevOps organization:

    ```powershell
    # PowerShell
    npx tfx-cli extension share `
      --publisher YOUR-PUBLISHER-ID `
      --extension-id devops-copilot-dev `
      --share-with YOUR-ORG-NAME `
      --token YOUR-MARKETPLACE-PAT
    ```

4. Install in Azure DevOps:
    - Go to **Organization Settings** → **Extensions** → **Shared** tab
    - Find **DevOps Copilot (Dev)** and click **Install**

5. For hot-rebuild during development:
    ```bash
    npm run dev
    ```
    This starts Webpack in watch mode. Repackage and republish (steps 1–2) or just refresh the page if the backend URL hasn't changed and only JS/CSS changed (once the extension is already installed, webpack watch rebuilds to `dist/` which the installed extension loads via `baseUri`).
    This starts Webpack in watch mode. You'll still need to refresh the Azure DevOps page to pick up changes.

## 4. Testing

### Backend Tests

```bash
cd backend
dotnet test Tests/DevOpsCopilot.Tests.csproj
```

### Extension Lint

```bash
cd extension
npm run lint
```

## 5. End-to-End Test

**GitHub Models / Standalone mode:**

1. Start the backend: `func start` in `backend/`
2. Start the dev server: `npm run dev` in `extension/`
3. Open `https://localhost:3000/standalone/standalone.html`
4. Enter GitHub PAT in Settings
5. Ask: _"Help me write acceptance criteria for a login feature"_

**Azure OpenAI / ADO Extension mode:**

1. Start the backend: `func start` in `backend/`
2. Sideload the extension in your test Azure DevOps organization
3. Navigate to **Azure Boards** → **Copilot** (the new hub page)
4. Try asking: _"Show me all active bugs"_

## Common Issues

**GitHub Models / Standalone:**

| Issue                                    | Solution                                                                     |
| ---------------------------------------- | ---------------------------------------------------------------------------- |
| Chat errors after a few messages         | Free tier rate limit hit. Wait ~1 minute or use `openai/gpt-4o-mini`         |
| `No GitHub Models API key` backend error | Set `GitHubModels__ApiKey` in settings or enter PAT in the UI Settings panel |
| Standalone page won't load               | Accept the self-signed cert at `https://localhost:3000` first                |
| DevOps tools unavailable                 | Add ADO Organization URL + ADO PAT in the Settings panel                     |

**Azure OpenAI / ADO Extension:**

| Issue                                    | Solution                                                            |
| ---------------------------------------- | ------------------------------------------------------------------- |
| `CORS error` in browser console          | Ensure `local.settings.json` has the correct `Host.CORS` origins    |
| `401 Unauthorized` from backend          | Check that the extension's OAuth token is being forwarded correctly |
| Extension doesn't appear in Azure DevOps | Make sure the `.vsix` was uploaded and the extension is installed   |
| `AzureOpenAI:Endpoint not configured`    | Check `local.settings.json` values                                  |
