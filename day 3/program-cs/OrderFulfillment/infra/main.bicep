// OrderFulfillment infra: the Container App running the API/worker host, plus
// a real Azure SQL server+database as the step-up from the local SQLite file
// the app runs on today. Follows the same conventions already proven in
// QuotesApi's infra: managed identity end to end, no connection-string
// secrets, free-tier-eligible SQL. No Service Bus module here — this app's
// event dispatch is in-process by design (see ADR-001), not broker-backed.
//
// Deploy with:
//   az deployment group create -g <rg> -f main.bicep -p params/dev.bicepparam
// Preview with:
//   az deployment group what-if -g <rg> -f main.bicep -p params/dev.bicepparam

@description('Environment name — drives resource naming and scale limits.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region for every resource in this deployment.')
param location string = resourceGroup().location

@description('Container image for the API/worker host, e.g. myregistry.azurecr.io/orderfulfillment-api:1.0.0.')
param containerImage string

@description('API container CPU cores.')
param apiCpuCores string = '0.25'

@description('API container memory.')
param apiMemorySize string = '0.5Gi'

@description('Minimum replica count (0 = scale-to-zero, fine for dev).')
param apiMinReplicas int = 0

@description('Maximum replica count under load.')
param apiMaxReplicas int = 1

@description('Set false to reuse an existing Container Apps Environment instead of creating a new one — most subscriptions cap how many Environments can exist at all.')
param createNewApiEnvironment bool = true

@description('Resource id of the existing Environment to reuse when createNewApiEnvironment is false.')
param existingApiEnvironmentId string = ''

@description('Resource id of the container registry the API image is pulled from.')
param containerRegistryId string

@description('SQL Server admin login.')
param sqlAdminLogin string

@secure()
@description('SQL Server admin password — always pass this at deploy time, never store it in a params file.')
param sqlAdminPassword string

var sqlServerName = 'sql-orderfulfillment-${environmentName}-${uniqueString(resourceGroup().id)}'
var sqlDatabaseName = 'orderfulfillment-${environmentName}'
var sqlServerFqdn = '${sqlServerName}${environment().suffixes.sqlServerHostname}'

// Same pattern as QuotesApi: the API authenticates via its own managed
// identity, never a password — this connection string carries no secret.
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;'

var containerAppName = 'ca-orderfulfillment-api-${environmentName}'
var environmentAppName = 'cae-orderfulfillment-${environmentName}'
var logAnalyticsName = 'log-orderfulfillment-${environmentName}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (createNewApiEnvironment) {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: environmentName == 'prod' ? 90 : 30
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = if (createNewApiEnvironment) {
  name: environmentAppName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.?properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

var appEnvironmentId = createNewApiEnvironment ? containerAppEnvironment.id : existingApiEnvironmentId

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

// User-assigned so its principal id exists before the Container App resource
// does — a system-assigned identity's principal id only exists once the
// Container App itself is created, but the platform tries to pull the image
// as part of that same creation, which is too late for a role grant.
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-orderfulfillment-api-${environmentName}'
  location: location
}

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
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${pullIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: appEnvironmentId
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
          name: 'orderfulfillment-api'
          image: containerImage
          resources: {
            cpu: json(apiCpuCores)
            memory: apiMemorySize
          }
          env: [
            { name: 'ConnectionStrings__DefaultConnection', value: sqlConnectionString }
          ]
        }
      ]
      scale: {
        minReplicas: apiMinReplicas
        maxReplicas: apiMaxReplicas
      }
    }
  }
  dependsOn: [
    acrPullAssignment
  ]
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Application'
      login: 'orderfulfillment-api-identity'
      sid: containerApp.identity.principalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: false
    }
    // Left publicly reachable (scoped to Azure services only) for now — the
    // Container App here has no VNet integration, so a private endpoint
    // would leave it unable to reach the database at all, breaking the
    // happy path this task is actually asking for. Locking this down with a
    // real private endpoint (the same pattern QuotesApi's sql.bicep already
    // proves out) is real, deliberate follow-up work, not an oversight.
    publicNetworkAccess: 'Enabled'
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    requestedBackupStorageRedundancy: 'Local'
    minCapacity: json('0.5')
    autoPauseDelay: 60
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

@description('The deployed API public URL.')
output apiUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

@description('The SQL server hostname.')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
