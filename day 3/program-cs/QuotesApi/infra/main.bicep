// Root deployment — wires the API, SQL, and Service Bus modules together for
// one environment (dev or prod, chosen via the matching params file).
// Deploy with:
//   az deployment group create -g <rg> -f main.bicep -p params/dev.bicepparam
// Preview with:
//   az deployment group what-if -g <rg> -f main.bicep -p params/dev.bicepparam

@description('Environment name — drives resource naming, SKU choices, and scale limits throughout every module.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region for every resource in this deployment.')
param location string = resourceGroup().location

@description('Container image for the API, e.g. myregistry.azurecr.io/quotes-api:1.0.0.')
param containerImage string

@description('API container CPU cores.')
param apiCpuCores string

@description('API container memory.')
param apiMemorySize string

@description('API minimum replica count (0 = scale-to-zero, fine for dev; prod should stay >= 1 to avoid cold starts).')
param apiMinReplicas int

@description('API maximum replica count under load.')
param apiMaxReplicas int

@description('Set false to reuse an existing Container Apps Environment (see modules/api.bicep) instead of creating a new one.')
param createNewApiEnvironment bool = true

@description('Resource id of the existing Environment to reuse when createNewApiEnvironment is false.')
param existingApiEnvironmentId string = ''

@description('Resource id of the container registry the API image is pulled from.')
param containerRegistryId string

@secure()
@description('Signing key for the "Internal" JWT auth scheme — the one app secret with no Managed Identity equivalent, stored in Key Vault and never passed to the app as a plain env value.')
param internalJwtSigningKey string

@description('Entra (Azure AD) tenant id used to validate Entra-issued tokens — not a secret, matches appsettings.json.')
param entraTenantId string

@description('Entra (Azure AD) application audience used to validate Entra-issued tokens — not a secret, matches appsettings.json.')
param entraAudience string

@description('SQL Server admin login.')
param sqlAdminLogin string

@secure()
@description('SQL Server admin password — always pass this at deploy time (CLI --parameters or a secure pipeline variable), never store it in a params file.')
param sqlAdminPassword string

@description('Set true to actually deploy Service Bus — left false by default since topics require the paid Standard tier with no free option, unlike the API and SQL pieces (both free-eligible). The module stays fully written and what-if-validated either way.')
param deployServiceBus bool = false

@description('Service Bus SKU — must be Standard or higher; topics are not available on Basic. Only used if deployServiceBus is true.')
param serviceBusSkuName string = 'Standard'

// Computed from the exact same naming expressions modules/sql.bicep and
// modules/servicebus.bicep use — deterministic, so the API's connection
// settings can be built here without waiting on those modules' outputs
// (which would create a circular module dependency, since both of those
// modules depend on the API's identity in the other direction).
// environment().suffixes.sqlServerHostname already includes its own leading
// dot (e.g. ".database.windows.net") — a real bug caught here the first time
// this deployed, producing a double dot in the FQDN.
var sqlServerFqdn = 'sql-quotes-${environmentName}-${uniqueString(resourceGroup().id)}${environment().suffixes.sqlServerHostname}'
var sqlDatabaseName = 'quotes-${environmentName}'
var serviceBusNamespaceHost = 'sb-quotes-${environmentName}-${uniqueString(resourceGroup().id)}.servicebus.windows.net'

// No password, no connection-string secret: Authentication=Active Directory
// Default makes Microsoft.Data.SqlClient fetch a token via the Container
// App's managed identity at connect time — the same identity granted SQL
// AAD admin rights in modules/sql.bicep.
var sqlConnectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;'

var baseAppSettings = [
  { name: 'ConnectionStrings__DefaultConnection', value: sqlConnectionString }
  { name: 'Entra__TenantId', value: entraTenantId }
  { name: 'Entra__Audience', value: entraAudience }
]

// Only set when Service Bus is actually deployed — DefaultAzureCredential
// plus this namespace host is all the app needs, no connection string,
// same principle as SQL above.
var appSettingsWithServiceBus = deployServiceBus
  ? concat(baseAppSettings, [{ name: 'ServiceBus__Namespace', value: serviceBusNamespaceHost }])
  : baseAppSettings

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-${environmentName}'
  params: {
    environmentName: environmentName
    location: location
    internalJwtSigningKey: internalJwtSigningKey
  }
}

module api 'modules/api.bicep' = {
  name: 'api-${environmentName}'
  params: {
    environmentName: environmentName
    location: location
    containerImage: containerImage
    cpuCores: apiCpuCores
    memorySize: apiMemorySize
    minReplicas: apiMinReplicas
    maxReplicas: apiMaxReplicas
    createNewEnvironment: createNewApiEnvironment
    existingEnvironmentId: existingApiEnvironmentId
    containerRegistryId: containerRegistryId
    keyVaultId: keyVault.outputs.vaultId
    keyVaultUri: keyVault.outputs.vaultUri
    appSettings: appSettingsWithServiceBus
  }
}

module network 'modules/network.bicep' = {
  name: 'network-${environmentName}'
  params: {
    environmentName: environmentName
    location: location
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql-${environmentName}'
  params: {
    environmentName: environmentName
    location: location
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    apiPrincipalId: api.outputs.principalId
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    privateDnsZoneId: network.outputs.privateDnsZoneId
  }
}

module serviceBus 'modules/servicebus.bicep' = if (deployServiceBus) {
  name: 'servicebus-${environmentName}'
  params: {
    environmentName: environmentName
    location: location
    skuName: serviceBusSkuName
    apiPrincipalId: api.outputs.principalId
  }
}

@description('The deployed API public URL.')
output apiUrl string = api.outputs.apiUrl

@description('The SQL server hostname, for building the app connection string.')
output sqlServerFqdn string = sql.outputs.sqlServerFqdn

@description('The Service Bus namespace hostname, empty when deployServiceBus is false.')
output serviceBusHost string = serviceBus.?outputs.serviceBusNamespaceHost ?? ''

@description('Subnet delegated to Container Instances, for a short-lived in-VNet connectivity check against the SQL private endpoint (not used by anything long-running).')
output connectivityCheckSubnetId string = network.outputs.connectivityCheckSubnetId
