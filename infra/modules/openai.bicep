// Azure OpenAI resource and model deployment

param location string
param baseName string
param deploymentName string
param modelName string
param modelVersion string

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: '${baseName}-openai'
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: '${baseName}-openai'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: deploymentName
  sku: {
    name: 'Standard'
    capacity: 30
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
}

output name string = openAi.name
output endpoint string = openAi.properties.endpoint
output id string = openAi.id
