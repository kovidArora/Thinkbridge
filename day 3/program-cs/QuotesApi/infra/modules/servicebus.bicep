// Service Bus module: namespace + the 'orders' topic + its two subscriptions.
// Mirrors ServiceBus-Demo's config.json exactly, but as real Azure resources
// instead of the local emulator that demo deliberately used to avoid this
// tier's real cost (Standard is required for topics — no free option).

@description('Environment name, used in resource naming and tagging.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Messaging units are not applicable to Standard tier; this only matters if upgraded to Premium later.')
param skuName string = 'Standard'

@description('Principal id of the API managed identity, granted Send/Listen roles below instead of a connection-string secret.')
param apiPrincipalId string

@description('Max delivery attempts before a message is dead-lettered — matches the 3 used in ServiceBus-Demo for a fast, observable demo; a real workload would typically use a higher value like 10.')
param maxDeliveryCount int = 10

var namespaceName = 'sb-quotes-${environmentName}-${uniqueString(resourceGroup().id)}'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
}

resource ordersTopic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: serviceBusNamespace
  name: 'orders'
  properties: {
    defaultMessageTimeToLive: 'PT1H'
  }
}

resource auditSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: ordersTopic
  name: 'audit-sub'
  properties: {
    lockDuration: 'PT30S'
    maxDeliveryCount: maxDeliveryCount
  }
}

resource processingSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: ordersTopic
  name: 'processing-sub'
  properties: {
    lockDuration: 'PT30S'
    maxDeliveryCount: maxDeliveryCount
  }
}

// Grant the API's managed identity Send + Listen — no connection string,
// same zero-secret principle as the real MI proxy already deployed.
var serviceBusDataOwnerRoleId = '090c5cfd-751d-490a-894a-3ce6f1109419'

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, apiPrincipalId, serviceBusDataOwnerRoleId)
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataOwnerRoleId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('The namespace fully-qualified hostname, used with DefaultAzureCredential instead of a connection string.')
output serviceBusNamespaceHost string = '${namespaceName}.servicebus.windows.net'
