param keyVaultName string
param principalId string
param tenantId string

// Key Vault Secrets User role — allows reading secrets (not managing them)
// This is the principle of least privilege: App Service can read secrets but not modify them
var keyVaultSecretsUserRoleDefinitionId = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, keyVaultSecretsUserRoleDefinitionId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      keyVaultSecretsUserRoleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
