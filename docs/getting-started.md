# Getting Started

This guide walks you through setting up a local development environment for the DevOps Copilot extension.

## Prerequisites

| Tool                       | Version | Install                                                                                               |
| -------------------------- | ------- | ----------------------------------------------------------------------------------------------------- |
| .NET SDK                   | 9.0+    | [Download](https://dotnet.microsoft.com/download/dotnet/9.0)                                          |
| Node.js                    | 20+     | [Download](https://nodejs.org/)                                                                       |
| Azure Functions Core Tools | v4      | [Install](https://learn.microsoft.com/azure/azure-functions/functions-run-local)                      |
| tfx-cli                    | latest  | `npm install -g tfx-cli`                                                                              |
| Azure OpenAI resource      | —       | [Create](https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAI) with a GPT-4o deployment |
| Azure DevOps org           | —       | [Create](https://dev.azure.com) a test organization                                                   |

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

Edit `local.settings.json`:

```json
{
    "Values": {
        "AzureOpenAI__Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
        "AzureOpenAI__DeploymentName": "gpt-4o",
        "AzureOpenAI__ApiKey": "YOUR-KEY",
        "AzureDevOps__DefaultOrganizationUrl": "https://dev.azure.com/YOUR-ORG"
    }
}
```

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

## 3. Extension Setup

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

1. Start the backend: `func start` in `backend/`
2. Sideload the extension in your test Azure DevOps organization
3. Navigate to **Azure Boards** → **Copilot** (the new hub page)
4. Try asking: "Show me all active bugs"

## Common Issues

| Issue                                    | Solution                                                            |
| ---------------------------------------- | ------------------------------------------------------------------- |
| `CORS error` in browser console          | Ensure `host.json` has the correct CORS origins                     |
| `401 Unauthorized` from backend          | Check that the extension's OAuth token is being forwarded correctly |
| Extension doesn't appear in Azure DevOps | Make sure the `.vsix` was uploaded and the extension is installed   |
| `AzureOpenAI:Endpoint not configured`    | Check `local.settings.json` values                                  |
