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

@description('Resource id of the container registry the image is pulled from — used to grant the Container App AcrPull rights so it can actually pull the image, without a registry password.')
param containerRegistryId string

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

// AcrPull role definition id (built-in, same across all subscriptions).
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

// User-assigned identity: created as its own resource, so its principal id
// exists (and can be granted AcrPull) *before* the Container App resource is
// even declared. A system-assigned identity's principal id only exists once
// the Container App itself is created — but the platform tries to pull the
// image as part of that same creation, so granting AcrPull afterwards is too
// late (the exact chicken-and-egg failure this module used to hit).
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-quotes-api-${environmentName}'
  location: location
}

// Registry is assumed to live in this same resource group (true for the real
// cr33kewg57w25su registry) — required so this can be declared `existing`
// and take a role assignment as a child/extension resource.
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: last(split(containerRegistryId, '/'))
}

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, pullIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    // Both identities: the user-assigned one exists early enough to pull the
    // image (see above); system-assigned is what SQL/Service Bus grant
    // access to below, kept separate so those role assignments don't need
    // to know about the registry concern at all.
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${pullIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: pullIdentity.id
        }
      ]
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
  dependsOn: [
    acrPullAssignment
  ]
}

@description('The system-assigned identity principal id — grant this access to SQL/Service Bus separately.')
output principalId string = containerApp.identity.principalId

@description('The publicly reachable URL of the deployed API.')
output apiUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
