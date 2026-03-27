using '../main.bicep'

// Flex Consumption (FC1) is only available in select regions.
// Supported regions include: eastus, eastus2, westus2, northeurope, westeurope, uksouth, eastasia, australiaeast
param location = 'swedencentral'
param baseName = 'devopscopilot'
param openAiDeploymentName = 'gpt-4o'
param openAiModelName = 'gpt-4o'
param openAiModelVersion = '2024-08-06'
