// ─────────────────────────────────────────────────────────
// DevOps Copilot — Main Bicep orchestrator
// Deploys all Azure resources needed for the backend.
// ─────────────────────────────────────────────────────────

targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name prefix for all resources (lowercase, no spaces)')
@minLength(3)
@maxLength(16)
param baseName string = 'devopscopilot'

@description('Azure OpenAI model deployment name')
param openAiDeploymentName string = 'gpt-4o'

@description('Azure OpenAI model name')
param openAiModelName string = 'gpt-4o'

@description('Azure OpenAI model version')
param openAiModelVersion string = '2024-08-06'

@description('Enable Key Vault purge protection (strongly recommended for production to prevent permanent data loss)')
param enablePurgeProtection bool = false

@description('Session memory provider for the Function App (inmemory or localfile)')
param memoryProvider string = 'inmemory'

@description('Default Azure DevOps organization URL')
param azureDevOpsOrgUrl string = 'https://dev.azure.com/miketran'

@description('Comma-separated CORS allowed origins. Include your publisher gallery CDN URLs.')
param corsAllowedOrigins string = 'https://dev.azure.com,https://*.visualstudio.com'

@description('Azure DevOps Marketplace publisher ID (e.g. MikeDemo). Used to validate extension app tokens.')
param extensionPublisherId string = ''

@description('Default model deployment name (also model ID). e.g. gpt-4o-mini')
param defaultModelDeployment string = 'gpt-4o-mini'

@description('Default model display name shown in the UI.')
param defaultModelDisplayName string = 'GPT-4o Mini'

@description('Default model description shown in the UI.')
param defaultModelDescription string = 'Fast and cost-effective for everyday tasks'

@description('Secondary model deployment name (also model ID). e.g. gpt-4o')
param secondaryModelDeployment string = 'gpt-4o'

@description('Secondary model display name shown in the UI.')
param secondaryModelDisplayName string = 'GPT-4o'

@description('Secondary model description shown in the UI.')
param secondaryModelDescription string = 'Most capable model for complex analysis'

// ─── Storage Account ───────────────────────────────────────

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    baseName: baseName
  }
}

// ─── Log Analytics + Application Insights ──────────────────

module monitoring 'modules/app-insights.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    baseName: baseName
  }
}

// ─── Key Vault ─────────────────────────────────────────────

module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    baseName: baseName
    enablePurgeProtection: enablePurgeProtection
  }
}

// ─── Azure OpenAI ──────────────────────────────────────────

module openAi 'modules/openai.bicep' = {
  name: 'openAi'
  params: {
    location: location
    baseName: baseName
    deploymentName: openAiDeploymentName
    modelName: openAiModelName
    modelVersion: openAiModelVersion
  }
}

// ─── Azure Functions ───────────────────────────────────────

module functionApp 'modules/function-app.bicep' = {
  name: 'functionApp'
  params: {
    location: location
    baseName: baseName
    storageAccountName: storage.outputs.name
    appInsightsConnectionString: monitoring.outputs.connectionString
    openAiEndpoint: openAi.outputs.endpoint
    openAiDeploymentName: openAiDeploymentName
    keyVaultName: keyVault.outputs.name
    memoryProvider: memoryProvider
    azureDevOpsOrgUrl: azureDevOpsOrgUrl
    corsAllowedOrigins: corsAllowedOrigins
    extensionPublisherId: extensionPublisherId
    defaultModelDeployment: defaultModelDeployment
    secondaryModelDeployment: secondaryModelDeployment
    defaultModelDisplayName: defaultModelDisplayName
    defaultModelDescription: defaultModelDescription
    secondaryModelDisplayName: secondaryModelDisplayName
    secondaryModelDescription: secondaryModelDescription
  }
}

// ─── Key Vault access for Function App ─────────────────────

module kvAccess 'modules/key-vault-access.bicep' = {
  name: 'kvAccess'
  params: {
    keyVaultName: keyVault.outputs.name
    principalId: functionApp.outputs.principalId
  }
}

// ─── Storage access for Function App (Managed Identity) ────

module storageAccess 'modules/storage-access.bicep' = {
  name: 'storageAccess'
  params: {
    storageAccountName: storage.outputs.name
    principalId: functionApp.outputs.principalId
  }
}

// ─── Outputs ───────────────────────────────────────────────

output functionAppName string = functionApp.outputs.name
output functionAppUrl string = functionApp.outputs.url
output openAiEndpoint string = openAi.outputs.endpoint
output keyVaultName string = keyVault.outputs.name
output appInsightsName string = monitoring.outputs.name
