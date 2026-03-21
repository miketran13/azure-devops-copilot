// Azure Key Vault for secrets management

param location string
param baseName string

@description('Enable purge protection (recommended for production to prevent accidental permanent deletion)')
param enablePurgeProtection bool = false

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${baseName}-kv'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: enablePurgeProtection
  }
}

output name string = keyVault.name
output uri string = keyVault.properties.vaultUri
