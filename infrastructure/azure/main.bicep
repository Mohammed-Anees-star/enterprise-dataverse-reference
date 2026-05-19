// ============================================================
// Enterprise Ticketing — Azure Infrastructure (Bicep IaC)
// Deploy with:
//   az group create -n rg-enterprise-ticketing-prod -l eastus
//   az deployment group create -g rg-enterprise-ticketing-prod -f main.bicep
//     -p environmentName=prod appName=enterprise-ticketing
// ============================================================

targetScope = 'resourceGroup'

@description('Environment name (dev, staging, prod)')
param environmentName string

@description('Application base name')
param appName string = 'enterprise-ticketing'

@description('Azure region')
param location string = resourceGroup().location

@description('Azure AD Tenant ID for Key Vault access policies')
param tenantId string = subscription().tenantId

var resourceSuffix = '${appName}-${environmentName}'
var tags = {
  Application: appName
  Environment: environmentName
  ManagedBy: 'Bicep'
  CreatedBy: 'PlatformEngineering'
}

// ============================================================
// APPLICATION INSIGHTS (deploy first — App Service needs the connection string)
// ============================================================
module appInsights 'app-insights.bicep' = {
  name: 'deploy-app-insights'
  params: {
    name: 'appi-${resourceSuffix}'
    location: location
    tags: tags
  }
}

// ============================================================
// KEY VAULT
// ============================================================
module keyVault 'key-vault.bicep' = {
  name: 'deploy-key-vault'
  params: {
    name: 'kv-${take(replace(resourceSuffix, '-', ''), 21)}'
    location: location
    tenantId: tenantId
    tags: tags
  }
}

// ============================================================
// SERVICE BUS
// ============================================================
module serviceBus 'service-bus.bicep' = {
  name: 'deploy-service-bus'
  params: {
    namespaceName: 'sb-${resourceSuffix}'
    location: location
    tags: tags
  }
}

// ============================================================
// APP SERVICE (deploy last — needs Key Vault URI and App Insights)
// ============================================================
module appService 'app-service.bicep' = {
  name: 'deploy-app-service'
  params: {
    appName: 'app-${resourceSuffix}'
    location: location
    tags: tags
    appInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultUri: keyVault.outputs.vaultUri
  }
  dependsOn: [ appInsights, keyVault ]
}

// Grant App Service Managed Identity access to Key Vault
module keyVaultAccess 'key-vault-access.bicep' = {
  name: 'deploy-key-vault-access'
  params: {
    keyVaultName: keyVault.outputs.vaultName
    principalId: appService.outputs.managedIdentityPrincipalId
    tenantId: tenantId
  }
  dependsOn: [ keyVault, appService ]
}

output apiUrl string = appService.outputs.defaultHostname
output keyVaultUri string = keyVault.outputs.vaultUri
output serviceBusNamespace string = serviceBus.outputs.namespaceName
