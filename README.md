# DevOps Copilot — AI-Powered Azure DevOps Extension

An open-source Azure DevOps extension that brings AI-powered copilot capabilities to Azure Boards. Search, analyze, create, and manage work items using natural language powered by **Microsoft Agent Framework** and **Azure OpenAI**.

## Features

- **AI Chat Hub** — Dedicated copilot page under Azure Boards for natural language interaction with your backlog
- **Work Item AI Panel** — Contextual AI assistant on every work item form (analyze requirements, suggest improvements)
- **Smart Actions** — Right-click context menu and toolbar actions: "Analyze with AI", "Generate Test Cases", "Suggest Child Items"
- **Multi-Agent Architecture** — Specialist agents (Search, Writer, Analyst) coordinated by an orchestrator for complex tasks
- **Secure by Design** — Token forwarding, Key Vault secrets, Managed Identity, no credentials stored client-side

## Architecture

```
┌─────────────────────────────────────────────┐
│  Azure DevOps Extension (TypeScript/React)  │
│   Hub │ Work Item Panel │ Context Menus     │
└──────────────────┬──────────────────────────┘
                   │ HTTPS (OAuth token forwarded)
                   ▼
┌─────────────────────────────────────────────┐
│  Azure Functions (.NET 9)                   │
│  Microsoft Agent Framework                  │
│                                             │
│  ┌────────────┐                             │
│  │Orchestrator│──┬──► SearchAgent           │
│  │   Agent    │  ├──► WriterAgent           │
│  └────────────┘  └──► AnalystAgent          │
│        │                    │               │
│        ▼                    ▼               │
│  Azure OpenAI         Azure DevOps          │
│  (GPT-4o)             REST API              │
└─────────────────────────────────────────────┘
```

## Project Structure

```
├── extension/          # Azure DevOps extension (TypeScript, React, Webpack)
├── backend/            # Azure Functions + Microsoft Agent Framework (.NET 9)
├── infra/              # Bicep IaC templates (Storage, Key Vault, OpenAI, Function App)
├── docs/               # Documentation
├── .github/workflows/  # CI/CD pipelines (ci, deploy-backend, deploy-extension)
└── .devcontainer/      # Dev Container / GitHub Codespaces support
```

---

## Run Locally

Follow these steps to get the full stack running on your machine for development and testing.

### Prerequisites

| Tool                       | Version | Install                                                                          |
| -------------------------- | ------- | -------------------------------------------------------------------------------- |
| .NET SDK                   | 9.0+    | [Download](https://dotnet.microsoft.com/download/dotnet/9.0)                     |
| Node.js                    | 20+     | [Download](https://nodejs.org/)                                                  |
| Azure Functions Core Tools | v4      | [Install](https://learn.microsoft.com/azure/azure-functions/functions-run-local) |
| tfx-cli                    | latest  | `npm install -g tfx-cli`                                                         |
| Azure CLI                  | latest  | [Install](https://learn.microsoft.com/cli/azure/install-azure-cli)               |

You also need:

- An **Azure OpenAI** resource with a **GPT-4o** deployment ([create one](https://portal.azure.com/#create/Microsoft.CognitiveServicesOpenAI))
- An **Azure DevOps** organization for testing ([create one](https://dev.azure.com))

> **Tip:** Use the included `.devcontainer/` for GitHub Codespaces or VS Code Dev Containers — all tools are pre-installed.

### Step 1 — Clone the Repository

```bash
git clone https://github.com/YOUR-ORG/devops-copilot.git
cd devops-copilot
```

### Step 2 — Configure & Start the Backend

```bash
cd backend

# Create local settings from the template
cp local.settings.example.json local.settings.json
```

Edit **`local.settings.json`** with your values:

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "AzureOpenAI__Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
        "AzureOpenAI__DeploymentName": "gpt-4o",
        "AzureOpenAI__ApiKey": "YOUR-AZURE-OPENAI-API-KEY",
        "AzureDevOps__DefaultOrganizationUrl": "https://dev.azure.com/YOUR-ORG",
        "Extension__SharedSecret": ""
    },
    "Host": {
        "CORS": "https://localhost:3000,https://dev.azure.com,https://*.visualstudio.com",
        "CORSCredentials": true
    }
}
```

> **Important:** The `Host.CORS` section is required for local development — `host.json`'s CORS settings are only applied when deployed to Azure. `func start` reads CORS from `local.settings.json`.

| Setting                               | Description                                                                                                     |
| ------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `AzureOpenAI__Endpoint`               | Your Azure OpenAI resource endpoint (Azure Portal → your resource → Keys & Endpoint)                            |
| `AzureOpenAI__DeploymentName`         | The model deployment name (e.g. `gpt-4o`)                                                                       |
| `AzureOpenAI__ApiKey`                 | API key from your Azure OpenAI resource. Leave blank to use `az login` credentials via `DefaultAzureCredential` |
| `AzureDevOps__DefaultOrganizationUrl` | Your Azure DevOps org URL, e.g. `https://dev.azure.com/myorg`                                                   |
| `Extension__SharedSecret`             | Leave empty for local dev (disables token validation). Set in production for security                           |

Build and start:

```bash
dotnet restore
dotnet build
func start
```

Verify the backend is running:

```bash
curl http://localhost:7071/api/health
# Should return: {"status":"healthy","timestamp":"..."}
```

### Step 3 — Build & Sideload the Extension

> **Important:** Azure DevOps **Services** (cloud at dev.azure.com) does **not** support uploading extensions from a local file. You must publish through the Visual Studio Marketplace, even for private dev extensions. ("Browse local extensions" only exists on self-hosted Azure DevOps Server.)

**3a — Create a Marketplace publisher account (one-time)**

1. Go to [marketplace.visualstudio.com/manage](https://marketplace.visualstudio.com/manage) and sign in with your Microsoft account
2. Click **Create publisher**, fill in a publisher ID (e.g. `mycompany-devops`) and display name, then save

**3b — Set your publisher ID in the extension manifest (one-time)**

Edit `extension/azure-devops-extension.json` and replace `YOUR-PUBLISHER-ID` with the ID you just created:

```json
{
  "publisher": "mycompany-devops",
  ...
}
```

**3c — Generate a Marketplace PAT (one-time)**

1. Go to your Azure DevOps organization → top-right avatar → **Personal Access Tokens**
2. Click **New Token**, give it a name (e.g. `marketplace-publish`)
3. Under **Scopes**, select **Custom defined** → enable **Marketplace → Publish** (under Marketplace → Manage)
4. Copy the token — you'll use it as `YOUR-MARKETPLACE-PAT` below

**3d — Build and package**

Open a new terminal:

```bash
cd extension
npm install
npm run package:dev
```

This creates `extension/dist/YOUR-PUBLISHER-ID.devops-copilot-dev-1.0.0.vsix`.

**3e — Publish as a private extension and install it**

1. Upload the `.vsix` to the Marketplace as **private**:

    ```powershell
    # PowerShell (Windows) — use (Get-Item ...) to expand the glob
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

    > **Note (PowerShell):** PowerShell does not expand `*.vsix` glob patterns. Use `(Get-Item dist/*.vsix).FullName` to resolve the actual filename, or pass it explicitly, e.g. `--vsix dist/MikeTtest.devops-copilot-dev-1.0.0.vsix`.

2. Share it with your Azure DevOps organization:

    ```powershell
    npx tfx-cli extension share `
      --publisher YOUR-PUBLISHER-ID `
      --extension-id devops-copilot-dev `
      --share-with YOUR-ORG-NAME `
      --token YOUR-MARKETPLACE-PAT
    ```

3. Install in Azure DevOps:
    - Go to **Organization Settings** → **Extensions** → **Shared** tab
    - Find **DevOps Copilot (Dev)** and click **Install**

### Step 4 — Use the Extension

1. Navigate to your Azure DevOps project → **Boards** → **Copilot** (new hub page)
2. Try natural language queries:
    - _"Show me all active bugs"_
    - _"Create a user story for adding login functionality"_
    - _"Analyze work item #123"_
3. Open any work item — you'll see a new **AI Copilot** panel in the form
4. Right-click any work item — you'll see new context menu actions

### Step 5 — Development Workflow

For active development `npm run dev` starts a webpack-dev-server over **HTTPS on port 3000** — the same URL the dev extension loads its iframes from.

```powershell
# Terminal 1: Backend API
cd backend
func start

# Terminal 2: Extension HTTPS dev server (rebuilds on every file save)
cd extension
npm run dev
```

**One-time: accept the self-signed certificate**

The dev server uses an auto-generated self-signed certificate. Azure DevOps won't load iframes from an untrusted origin, so you need to trust the cert once in your browser:

1. Open **`https://localhost:3000`** in the same browser you use for Azure DevOps
2. You'll see a browser security warning — click **Advanced → Proceed to localhost (unsafe)** (Chrome) or **Accept the Risk and Continue** (Firefox)
3. You should see a directory listing or blank page — that's fine, it just confirms the cert is trusted
4. Go back to Azure DevOps and refresh — the extension iframes will now load

After the extension rebuilds (on any file save), refresh the Azure DevOps page to pick up changes.

**Run backend tests:**

```powershell
cd backend/Tests
dotnet test
```

### Troubleshooting (Local)

| Issue                                           | Solution                                                                                                                                                                            |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CORS error` / preflight blocked                | Add a `"Host": { "CORS": "https://localhost:3000,...", "CORSCredentials": true }` section to `local.settings.json` — `host.json` CORS is only applied in Azure, not by `func start` |
| `401 Unauthorized` from backend                 | Set `Extension__SharedSecret` to empty string in `local.settings.json` to disable token validation for local dev                                                                    |
| Extension doesn't appear in Azure DevOps        | The extension must be published to Marketplace as private, shared with your org, and installed. Check **Organization Settings → Extensions → Shared**                               |
| Work item form group / context menu not loading | You must accept the self-signed cert at `https://localhost:3000` first (see Step 5 above). Without this, ADO silently refuses to load the iframes                                   |
| Rule Engine errors on work item form            | Usually harmless ADO platform noise when AreaPath/IterationPath trees aren't fully loaded. Unrelated to the extension — refresh the page                                            |
| `AzureOpenAI:Endpoint not configured`           | Check all `AzureOpenAI__*` values in `local.settings.json`                                                                                                                          |
| `npm install` fails with peer dep errors        | Run `npm install --legacy-peer-deps` (or use the included `.npmrc` which sets this automatically)                                                                                   |
| Backend port conflict                           | Azure Functions Core Tools defaults to port 7071. If in use, run `func start --port 7072` and update `DEFAULT_BACKEND_URL` in `extension/src/services/backendApi.ts`                |

---

## Publish to Azure

Follow these steps to deploy the backend to Azure and publish the extension to the Visual Studio Marketplace.

### Prerequisites

- An Azure subscription with permission to create resources
- Azure CLI installed and authenticated: `az login`
- A [Visual Studio Marketplace publisher account](https://marketplace.visualstudio.com/manage) (free to create)

### Step 1 — Deploy Azure Infrastructure

Create all required Azure resources (Function App, Azure OpenAI, Key Vault, Storage, App Insights) using the provided Bicep templates:

```bash
# Create resource group
az group create --name devops-copilot-rg --location eastus2

# Deploy infrastructure
az deployment group create \
  --resource-group devops-copilot-rg \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam
```

> **What gets created:** Storage Account, Log Analytics + Application Insights, Key Vault, Azure OpenAI (with GPT-4o deployment), Function App on Premium EP1 plan with Managed Identity.

After deployment, note the **Function App name** and **URL** from the output. Then store the OpenAI API key in Key Vault:

```bash
# Get the deployed resource names (adjust if you customized baseName)
FUNC_APP_NAME="devopscopilot-func"   # from Bicep output
KV_NAME="devopscopilot-kv"           # from Bicep output

# Store secrets in Key Vault
az keyvault secret set --vault-name $KV_NAME --name openai-api-key --value "YOUR-OPENAI-API-KEY"
```

> **Cost Tip:** For development/testing, edit `infra/parameters/dev.bicepparam` and change `functionPlanSku` from `'EP1'` to `'Y1'` (Consumption plan) to reduce costs. Be aware of cold starts on Consumption plan.

### Step 2 — Deploy the Backend Function App

```bash
cd backend

# Build for release
dotnet publish -c Release -o ./publish

# Deploy to Azure
func azure functionapp publish $FUNC_APP_NAME --dotnet-isolated
```

Verify the deployment:

```bash
curl https://$FUNC_APP_NAME.azurewebsites.net/api/health
# Should return: {"status":"healthy","timestamp":"..."}
```

**Configure application settings** (if not sourced from Key Vault):

```bash
az functionapp config appsettings set \
  --name $FUNC_APP_NAME \
  --resource-group devops-copilot-rg \
  --settings \
    "AzureOpenAI__Endpoint=https://YOUR-RESOURCE.openai.azure.com/" \
    "AzureOpenAI__DeploymentName=gpt-4o" \
    "AzureDevOps__DefaultOrganizationUrl=https://dev.azure.com/YOUR-ORG" \
    "Extension__SharedSecret=GENERATE-A-STRONG-SECRET-HERE"
```

> The Bicep templates configure the Function App to read the OpenAI API key from Key Vault via Managed Identity. You only need to set `AzureOpenAI__ApiKey` manually if not using Key Vault.

### Step 3 — Update the Extension Backend URL

Before publishing the extension, point it to your Azure-hosted backend.

Edit **`extension/src/services/backendApi.ts`** and change the default URL:

```typescript
const DEFAULT_BACKEND_URL = "https://devopscopilot-func.azurewebsites.net/api";
```

Replace `devopscopilot-func` with your actual Function App name.

### Step 4 — Create a Marketplace Publisher

1. Go to [Visual Studio Marketplace Publishing Portal](https://marketplace.visualstudio.com/manage)
2. Sign in with your Microsoft account
3. Click **Create publisher** and choose a publisher ID (e.g., `your-company-name`)
4. Edit **`extension/azure-devops-extension.json`** and update the `publisher` field:

```json
{
  "publisher": "your-company-name",
  ...
}
```

### Step 5 — Build & Publish the Extension

```bash
cd extension
npm install
npm run build

# Package the extension
npx tfx-cli extension create \
  --manifest-globs dist/azure-devops-extension.json \
  --output-path ./dist
```

**Option A — Publish publicly to the Marketplace:**

```powershell
# PowerShell
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

> Generate a PAT at https://dev.azure.com with **Marketplace → Manage** scope.

**Option B — Share privately with specific organizations (recommended for testing):**

```powershell
# PowerShell
npx tfx-cli extension publish `
  --vsix (Get-Item dist/*.vsix).FullName `
  --token YOUR-MARKETPLACE-PAT

npx tfx-cli extension share `
  --publisher your-company-name `
  --extension-id devops-copilot `
  --share-with your-org-name `
  --token YOUR-MARKETPLACE-PAT
```

```bash
# bash / macOS / Linux
npx tfx-cli extension publish \
  --vsix dist/*.vsix \
  --token YOUR-MARKETPLACE-PAT

npx tfx-cli extension share \
  --publisher your-company-name \
  --extension-id devops-copilot \
  --share-with your-org-name \
  --token YOUR-MARKETPLACE-PAT
```

Then install in the target organization: **Organization Settings** → **Extensions** → **Shared** → find and install.

### Step 6 — Configure CORS for Production

Ensure the Function App allows requests from Azure DevOps. The Bicep template configures this automatically, but you can verify:

```bash
az functionapp cors show --name $FUNC_APP_NAME --resource-group devops-copilot-rg
```

Expected allowed origins: `https://dev.azure.com`, `https://*.visualstudio.com`

If missing:

```bash
az functionapp cors add --name $FUNC_APP_NAME --resource-group devops-copilot-rg \
  --allowed-origins "https://dev.azure.com"
```

### Step 7 — Post-Deployment Checklist

- [ ] Azure OpenAI endpoint is accessible and GPT-4o deployment is active
- [ ] Key Vault has the `openai-api-key` secret
- [ ] Function App has system-assigned Managed Identity enabled
- [ ] Function App has **Key Vault Secrets User** role on the Key Vault
- [ ] Function App CORS allows `https://dev.azure.com`
- [ ] `Extension__SharedSecret` is set in Function App app settings (and matches the extension if you implement custom token signing)
- [ ] Extension is installed in the target Azure DevOps organization
- [ ] Health check passes: `curl https://<func-app>.azurewebsites.net/api/health`
- [ ] Open the **Copilot** hub in Azure Boards and test a query

### Automated CI/CD (GitHub Actions)

The repository includes three GitHub Actions workflows for automated deployment:

| Workflow             | File                                     | Trigger             | What it does                                             |
| -------------------- | ---------------------------------------- | ------------------- | -------------------------------------------------------- |
| **CI**               | `.github/workflows/ci.yml`               | PR & push to `main` | Builds backend + extension, runs tests                   |
| **Deploy Backend**   | `.github/workflows/deploy-backend.yml`   | Push to `main`      | Deploys Bicep infra + publishes Function App             |
| **Deploy Extension** | `.github/workflows/deploy-extension.yml` | Push to `main`      | Builds, packages, and publishes extension to Marketplace |

**Required GitHub secrets/variables:**

| Secret                  | Description                                               |
| ----------------------- | --------------------------------------------------------- |
| `AZURE_CLIENT_ID`       | Service principal / App Registration client ID (for OIDC) |
| `AZURE_TENANT_ID`       | Azure AD tenant ID                                        |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID                                     |
| `MARKETPLACE_PAT`       | VS Marketplace Personal Access Token                      |

| Variable         | Description                      |
| ---------------- | -------------------------------- |
| `RESOURCE_GROUP` | Target Azure resource group name |
| `ENVIRONMENT`    | `dev` or `prod`                  |

See [docs/deployment.md](docs/deployment.md) for OIDC federation setup details.

### Estimated Monthly Costs

| Resource             | SKU               | Estimated Cost                                     |
| -------------------- | ----------------- | -------------------------------------------------- |
| Azure Functions      | Premium EP1       | ~$130/mo                                           |
| Azure OpenAI         | S0 + GPT-4o usage | Pay-per-token (~$0.005/1K input, $0.015/1K output) |
| Key Vault            | Standard          | ~$0.03/10K operations                              |
| Application Insights | Pay-as-you-go     | ~$2.30/GB ingested                                 |
| Storage Account      | Standard LRS      | ~$0.02/GB                                          |

> Use the Consumption plan (`Y1`) instead of Premium (`EP1`) for dev/test to reduce to ~$0/mo + per-execution costs.

---

## Documentation

- [Getting Started](docs/getting-started.md) — detailed local development setup
- [Architecture](docs/architecture.md) — design decisions, agent system, data flow
- [Deployment](docs/deployment.md) — full Azure deployment guide
- [Extending](docs/extending.md) — adding new agents, tools, and extension points

## Contributing

We welcome contributions! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

[MIT](LICENSE)
