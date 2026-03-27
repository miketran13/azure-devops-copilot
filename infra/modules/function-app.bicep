// Azure Functions — Consumption Plan (Y1/Dynamic, Linux) + Function App
// Y1 is the standard serverless plan: pay-per-execution, universally available.

param location string
param baseName string
param storageAccountName string
param appInsightsConnectionString string
param openAiEndpoint string
param openAiDeploymentName string
param keyVaultName string
@description('Session memory provider. inmemory = no persistence (recommended for Consumption plan). localfile = writes to /tmp/sessions.')
param memoryProvider string = 'inmemory'
@description('Default Azure DevOps organization URL, e.g. https://dev.azure.com/myorg')
param azureDevOpsOrgUrl string
@description('Comma-separated list of allowed CORS origins. Include your publisher gallery CDN URLs.')
param corsAllowedOrigins string
@description('Azure DevOps Marketplace publisher ID used to validate extension app tokens.')
param extensionPublisherId string
@description('Default model deployment name (also used as model ID). e.g. gpt-4o-mini')
param defaultModelDeployment string = 'gpt-4o-mini'
@description('Default model display name shown in the UI.')
param defaultModelDisplayName string = 'GPT-4o Mini'
@description('Default model description shown in the UI.')
param defaultModelDescription string = 'Fast and cost-effective for everyday tasks'
@description('Secondary model deployment name (also used as model ID). e.g. gpt-4o')
param secondaryModelDeployment string = 'gpt-4o'
@description('Secondary model display name shown in the UI.')
param secondaryModelDisplayName string = 'GPT-4o'
@description('Secondary model description shown in the UI.')
param secondaryModelDescription string = 'Most capable model for complex analysis'

// Reference existing storage account to retrieve its key locally.
// This avoids passing secrets through module outputs (bicep linter: outputs-should-not-contain-secrets).
resource existingStorage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${existingStorage.listKeys().keys[0].value};EndpointSuffix=core.windows.net'

// ─── Consumption Plan (Linux) ──────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${baseName}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// ─── Function App ──────────────────────────────────────────

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${baseName}-func'
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|9'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      cors: {
        allowedOrigins: split(corsAllowedOrigins, ',')
        supportCredentials: true
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          // Set by the deploy workflow to the blob SAS URL of the deployed zip package.
          // Bicep sets it to '1' as a placeholder; the deploy step overrides with the real URL.
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'AzureOpenAI__Endpoint'
          value: openAiEndpoint
        }
        {
          name: 'AzureOpenAI__DefaultDeployment'
          value: defaultModelDeployment
        }
        {
          name: 'Extension__SharedSecret'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=extension-shared-secret)'
        }
        {
          name: 'AzureOpenAI__ApiKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=openai-api-key)'
        }
        {
          name: 'AzureOpenAI__Models__0__Id'
          value: defaultModelDeployment
        }
        {
          name: 'AzureOpenAI__Models__0__DeploymentName'
          value: defaultModelDeployment
        }
        {
          name: 'AzureOpenAI__Models__0__DisplayName'
          value: defaultModelDisplayName
        }
        {
          name: 'AzureOpenAI__Models__0__Description'
          value: defaultModelDescription
        }
        {
          name: 'AzureOpenAI__Models__0__IsDefault'
          value: 'true'
        }
        {
          name: 'AzureOpenAI__Models__1__Id'
          value: secondaryModelDeployment
        }
        {
          name: 'AzureOpenAI__Models__1__DeploymentName'
          value: secondaryModelDeployment
        }
        {
          name: 'AzureOpenAI__Models__1__DisplayName'
          value: secondaryModelDisplayName
        }
        {
          name: 'AzureOpenAI__Models__1__Description'
          value: secondaryModelDescription
        }
        {
          name: 'AzureOpenAI__Models__1__IsDefault'
          value: 'false'
        }
        {
          name: 'Extension__PublisherId'
          value: extensionPublisherId
        }
        {
          name: 'AzureDevOps__DefaultOrganizationUrl'
          value: azureDevOpsOrgUrl
        }
        {
          name: 'Cors__AllowedOrigins'
          value: corsAllowedOrigins
        }
        {
          // Controls session persistence. Override via Azure App Setting or GitHub variable.
          // localfile writes to /tmp/sessions (writable on Linux). wwwroot is read-only.
          name: 'Memory__Provider'
          value: memoryProvider
        }
        {
          // Disable Kudu/Oryx remote build — we deploy pre-compiled .NET output.
          // Without this, Linux Kudu tries to build the zip contents and returns 400 Bad Request.
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'ENABLE_ORYX_BUILD'
          value: 'false'
        }
      ]
    }
  }
}

// ─── Disable FTP basic authentication ─────────────────────
// SCM (Kudu) basic auth is kept enabled so zip deploy works.
// FTP access is disabled as it is not needed.

resource ftpBasicAuthPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  name: 'ftp'
  parent: functionApp
  properties: {
    allow: false
  }
}

output name string = functionApp.name
output url string = 'https://${functionApp.properties.defaultHostName}'
output principalId string = functionApp.identity.principalId