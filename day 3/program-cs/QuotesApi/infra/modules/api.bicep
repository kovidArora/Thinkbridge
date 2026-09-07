// API module: Container Apps Environment + the quotes-api Container App itself.
// Mirrors the real, manually-created quotes-api Container App this project
// already runs — this module is what *should* have created it, had IaC been
// used from the start instead of `azd`/`az` CLI commands run by hand.

@description('Environment name, used in resource naming and tagging.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Container image to deploy, e.g. myregistry.azurecr.io/quotes-api:1.0.0.')
param containerImage string

@description('CPU cores allocated to the container (fractional allowed, e.g. 0.5).')
param cpuCores string

@description('Memory allocated to the container, e.g. "1Gi".')
param memorySize string

@description('Minimum number of replicas — 0 allows scale-to-zero for dev.')
param minReplicas int

@description('Maximum number of replicas under load.')
param maxReplicas int

@description('Non-secret application settings (connection strings/keys go through Key Vault references instead, not plain env vars).')
param appSettings array = []

@description('Set false to reuse an existing Container Apps Environment instead of creating a new one — most subscriptions (including this one, on the free trial) cap how many Environments can exist at all, so sharing one across dev/prod apps is the realistic default, not a workaround.')
param createNewEnvironment bool = true

@description('Resource id of an existing Container Apps Environment to deploy into when createNewEnvironment is false.')
param existingEnvironmentId string = ''

var containerAppName = 'ca-quotes-api-${environmentName}'
var environmentAppName = 'cae-quotes-${environmentName}'
var logAnalyticsName = 'log-quotes-${environmentName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (createNewEnvironment) {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    // Dev doesn't need long retention; prod does, for real incident investigation.
    retentionInDays: environmentName == 'prod' ? 90 : 30
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = if (createNewEnvironment) {
  name: environmentAppName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.?properties.customerId
        // .listKeys() can't take the safe-dereference operator (Bicep BCP322) —
        // this line only ever runs when logAnalytics also exists, since both
        // are gated by the same createNewEnvironment condition; the warning
        // below is a known false positive for this exact paired-condition pattern.
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

var environmentId = createNewEnvironment ? containerAppEnvironment.id : existingEnvironmentId

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    // System-assigned identity: no client secret needed to reach SQL/Service
    // Bus below — same principle already proven for the real MI proxy.
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
    }
    template: {
      containers: [
        {
          name: 'quotes-api'
          image: containerImage
          resources: {
            cpu: json(cpuCores)
            memory: memorySize
          }
          env: appSettings
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

@description('The system-assigned identity principal id — grant this access to SQL/Service Bus separately.')
output principalId string = containerApp.identity.principalId

@description('The publicly reachable URL of the deployed API.')
output apiUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
