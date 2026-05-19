// App Service Plan + Web App for Enterprise Ticketing API

param appName string
param location string
param tags object
param appInsightsConnectionString string
param keyVaultUri string

@description('App Service Plan SKU — P2v3 recommended for production (2 cores, 8GB)')
param skuName string = 'P2v3'
param skuTier string = 'PremiumV3'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: 'plan-${appName}'
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  kind: 'linux'
  properties: {
    reserved: true  // Required for Linux
  }
}

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'  // Managed Identity — no credentials in code
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health/live'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVault__VaultUri'
          value: keyVaultUri
        }
        // Secrets are retrieved from Key Vault at runtime via Managed Identity
        // Pattern: @Microsoft.KeyVault(SecretUri=https://vault.vault.azure.net/secrets/secret-name/)
        {
          name: 'Dataverse__ClientSecret'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/dataverse-client-secret/)'
        }
        {
          name: 'ServiceBus__ConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/servicebus-connection-string/)'
        }
      ]
    }
  }
}

// Deployment Slot — staging
resource stagingSlot 'Microsoft.Web/sites/slots@2023-01-01' = {
  name: 'staging'
  parent: webApp
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      healthCheckPath: '/health/live'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Staging'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ]
    }
  }
}

output defaultHostname string = webApp.properties.defaultHostName
output managedIdentityPrincipalId string = webApp.identity.principalId
output stagingSlotHostname string = stagingSlot.properties.defaultHostName
