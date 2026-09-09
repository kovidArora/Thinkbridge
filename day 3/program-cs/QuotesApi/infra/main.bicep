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

@description('SQL Server admin login.')
param sqlAdminLogin string

@secure()
@description('SQL Server admin password — always pass this at deploy time (CLI --parameters or a secure pipeline variable), never store it in a params file.')
param sqlAdminPassword string

@description('Set true to actually deploy Service Bus — left false by default since topics require the paid Standard tier with no free option, unlike the API and SQL pieces (both free-eligible). The module stays fully written and what-if-validated either way.')
param deployServiceBus bool = false

@description('Service Bus SKU — must be Standard or higher; topics are not available on Basic. Only used if deployServiceBus is true.')
param serviceBusSkuName string = 'Standard'

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
