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

@description('Azure Functions hosting plan SKU (EP1 recommended for AI workloads)')
@allowed(['EP1', 'EP2', 'EP3'])
param functionPlanSku string = 'EP1'

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
    planSku: functionPlanSku
    storageConnectionString: storage.outputs.connectionString
    appInsightsConnectionString: monitoring.outputs.connectionString
    openAiEndpoint: openAi.outputs.endpoint
    openAiDeploymentName: openAiDeploymentName
    keyVaultName: keyVault.outputs.name
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

// ─── Outputs ───────────────────────────────────────────────

output functionAppName string = functionApp.outputs.name
output functionAppUrl string = functionApp.outputs.url
output openAiEndpoint string = openAi.outputs.endpoint
output keyVaultName string = keyVault.outputs.name
output appInsightsName string = monitoring.outputs.name
