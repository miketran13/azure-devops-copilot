# Deployment Guide

## Prerequisites

- Azure subscription with permission to create resources
- Azure CLI installed and authenticated (`az login`)
- GitHub repository with Actions enabled
- A Visual Studio Marketplace publisher account (for extension publishing)

## 1. Deploy Azure Infrastructure

### Option A: Azure CLI (One-time setup)

```bash
# Create resource group
az group create --name devops-copilot-rg --location eastus2

# Deploy all resources
az deployment group create \
  --resource-group devops-copilot-rg \
  --template-file infra/main.bicep \
  --parameters infra/parameters/dev.bicepparam

# Store the Azure OpenAI API key in Key Vault
az keyvault secret set \
  --vault-name devopscopilot-kv \
  --name openai-api-key \
  --value "YOUR-OPENAI-API-KEY"
```

### Option B: GitHub Actions (Automated)

1. Create an Azure AD App Registration for OIDC:

    ```bash
    az ad app create --display-name "devops-copilot-deploy"
    ```

2. Configure federated credentials for GitHub Actions OIDC.

3. Add these GitHub repository secrets/variables:

    | Secret                  | Value                      |
    | ----------------------- | -------------------------- |
    | `AZURE_CLIENT_ID`       | App Registration client ID |
    | `AZURE_TENANT_ID`       | Azure AD tenant ID         |
    | `AZURE_SUBSCRIPTION_ID` | Azure subscription ID      |

    | Variable         | Value               |
    | ---------------- | ------------------- |
    | `RESOURCE_GROUP` | `devops-copilot-rg` |
    | `ENVIRONMENT`    | `dev` or `prod`     |

4. Push to `main` — the `deploy-backend.yml` workflow will deploy automatically.

## 2. Deploy the Function App

### Manual Deployment

```bash
cd backend
dotnet publish -c Release -o ./publish
func azure functionapp publish devopscopilot-func --dotnet-isolated
```

### Automated (GitHub Actions)

The `deploy-backend.yml` workflow handles this automatically on push to `main`.

## 3. Publish the Extension

### Create a Publisher

1. Go to https://marketplace.visualstudio.com/manage
2. Create a new publisher (e.g., `your-publisher-id`)
3. Update `extension/azure-devops-extension.json` → set `"publisher"` to your publisher ID

### Package & Publish

```bash
cd extension
npm ci
npm run build

# Package
npx tfx-cli extension create \
  --manifest-globs dist/azure-devops-extension.json \
  --output-path ./dist

# Publish (first time = create, subsequent = update)
npx tfx-cli extension publish \
  --vsix dist/*.vsix \
  --token YOUR-MARKETPLACE-PAT
```

### Share Privately (for testing)

```bash
npx tfx-cli extension share \
  --publisher your-publisher-id \
  --extension-id devops-copilot \
  --share-with your-org-name \
  --token YOUR-MARKETPLACE-PAT
```

Then install in your test organization: **Organization Settings** → **Extensions** → **Shared** → Install.

### Automated (GitHub Actions)

Add the `MARKETPLACE_PAT` secret to your repository. The `deploy-extension.yml` workflow publishes on push to `main`.

## 4. Configure the Extension

After installing the extension in your Azure DevOps organization:

1. Navigate to **Azure Boards** → **Copilot**
2. The extension connects to the backend URL configured in the code
3. For production, update the backend URL in `extension/src/services/backendApi.ts` before building

## 5. Post-Deployment Checklist

- [ ] Azure OpenAI endpoint is accessible
- [ ] Key Vault has the `openai-api-key` secret
- [ ] Function App has system-assigned Managed Identity
- [ ] Function App has Key Vault Secrets User role on Key Vault
- [ ] CORS is configured for `https://dev.azure.com`
- [ ] Extension is installed in target Azure DevOps organization
- [ ] Health check passes: `curl https://devopscopilot-func.azurewebsites.net/api/health`

## Resource Costs (Estimate)

| Resource             | SKU               | Estimated Monthly Cost                             |
| -------------------- | ----------------- | -------------------------------------------------- |
| Azure Functions      | Premium EP1       | ~$130/mo                                           |
| Azure OpenAI         | S0 + GPT-4o usage | Pay per token (~$0.005/1K input, $0.015/1K output) |
| Key Vault            | Standard          | ~$0.03/10K operations                              |
| Application Insights | Pay-as-you-go     | ~$2.30/GB ingested                                 |
| Storage Account      | Standard LRS      | ~$0.02/GB                                          |

> **Cost Tip:** For development/testing, consider using the Consumption plan instead of Premium EP1 (modify `functionPlanSku` in the Bicep parameters). Be aware of cold starts and timeout limitations.
