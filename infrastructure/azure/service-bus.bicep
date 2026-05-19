param namespaceName string
param location string
param tags object

@description('Service Bus SKU — Standard for dev/staging, Premium for production (isolation, geo-redundancy)')
param skuName string = 'Standard'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    zoneRedundant: skuName == 'Premium'
    minimumTlsVersion: '1.2'
    disableLocalAuth: false
  }
}

resource ticketEventsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'ticket-events'
  parent: serviceBusNamespace
  properties: {
    maxDeliveryCount: 10           // After 10 failed attempts → Dead Letter Queue
    lockDuration: 'PT5M'           // Consumer has 5 minutes to complete message
    defaultMessageTimeToLive: 'P7D' // Messages expire after 7 days
    deadLetteringOnMessageExpiration: true
    enableBatchedOperations: true
    requiresDuplicateDetection: false
    duplicateDetectionHistoryTimeWindow: 'PT10M'
  }
}

resource notificationsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'ticket-notifications'
  parent: serviceBusNamespace
  properties: {
    maxDeliveryCount: 5
    lockDuration: 'PT2M'
    defaultMessageTimeToLive: 'P1D'
    deadLetteringOnMessageExpiration: true
    enableBatchedOperations: true
  }
}

output namespaceName string = serviceBusNamespace.name
output namespaceConnectionString string = listkeys(
  '${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey',
  serviceBusNamespace.apiVersion
).primaryConnectionString
