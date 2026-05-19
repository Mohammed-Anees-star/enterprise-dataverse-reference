param name string
param location string
param tenantId string
param tags object

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true   // Use RBAC (not legacy access policies)
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true     // Prevents accidental permanent deletion
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    networkAcls: {
      defaultAction: 'Allow'        // In production: 'Deny' with specific IP rules
      bypass: 'AzureServices'
    }
  }
}

output vaultUri string = keyVault.properties.vaultUri
output vaultName string = keyVault.name
