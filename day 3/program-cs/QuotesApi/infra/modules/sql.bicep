// SQL module: Azure SQL logical server + database.
// The real app runs on SQLite today (a local file) — this models the real
// managed-SQL step-up a genuine production deployment would need, since a
// SQLite file living in a container's ephemeral disk is exactly the bug
// already found and documented (it doesn't survive a container restart).

@description('Environment name, used in resource naming and tagging.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('SQL Server admin login — the password is supplied via a secure param, never stored in this file.')
param sqlAdminLogin string

@secure()
@description('SQL Server admin password. Pass via --parameters sqlAdminPassword=$env:SQL_ADMIN_PASSWORD at deploy time, never committed in a params file.')
param sqlAdminPassword string

@description('Database SKU name — General Purpose Serverless (GP_S_Gen5) for both dev and prod, so both can use the Azure SQL free offer (useFreeLimit below) instead of either one costing real money.')
param skuName string = 'GP_S_Gen5'

@description('Database SKU tier — must be GeneralPurpose to use the free offer.')
param skuTier string = 'GeneralPurpose'

@description('Serverless vCores (0.5-4 for the free-offer-eligible range).')
param vCores int = 1

@description('Minutes of inactivity before serverless auto-pauses (minimum 60) — reduces compute usage against the free monthly grant even further.')
param autoPauseDelayMinutes int = 60

@description('Principal id of the API managed identity, granted db_datareader/db_datawriter via AAD auth instead of a connection-string password.')
param apiPrincipalId string

@description('Subnet to land the private endpoint NIC in — from modules/network.bicep.')
param privateEndpointSubnetId string

@description('Private DNS zone (privatelink.database.windows.net) the endpoint registers into — from modules/network.bicep.')
param privateDnsZoneId string

var sqlServerName = 'sql-quotes-${environmentName}-${uniqueString(resourceGroup().id)}'
var sqlDatabaseName = 'quotes-${environmentName}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    // The API authenticates via its managed identity (Azure AD), not the
    // SQL login above — that login exists only for break-glass admin access.
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Application'
      login: 'quotes-api-identity'
      sid: apiPrincipalId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: false
    }
    // No firewall rules to punch holes in, no public IP to reach at all —
    // the only path in is the private endpoint below. The real app
    // (hardcoded to SQLite today) never actually queries this server, so
    // there's nothing running that this could break.
    publicNetworkAccess: 'Disabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: skuName
    tier: skuTier
    family: 'Gen5'
    capacity: vCores
  }
  properties: {
    // Dev can be thrown away; prod gets real geo-redundant backups.
    // The free-limit offer (useFreeLimit below) only allows Local backup
    // storage redundancy, even for prod — Geo is rejected outright
    // (ProvisioningDisabled), so this can't vary by environment here.
    requestedBackupStorageRedundancy: 'Local'
    minCapacity: json('0.5')
    autoPauseDelay: autoPauseDelayMinutes
    // The actual free-offer opt-in: up to 10 serverless databases per
    // subscription get 100,000 free vCore-seconds + 32GB storage/month,
    // forever, no expiration. AutoPause (not BillOverUsage) means this can
    // never incur a real charge even if the monthly grant is exceeded —
    // it just pauses until next month instead of billing.
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
  }
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-${sqlServerName}'
  location: location
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'pe-connection-${sqlServerName}'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: ['sqlServer']
        }
      }
    ]
  }
}

// Auto-registers the private IP under <sqlServerName>.privatelink.database.windows.net
// in the shared zone — without this, DNS resolution inside the VNet would
// still return nothing for the private endpoint's hostname.
resource privateDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'privatelink-database-windows-net'
        properties: {
          privateDnsZoneId: privateDnsZoneId
        }
      }
    ]
  }
}

@description('Fully qualified server hostname, for building the connection string.')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The database name.')
output databaseName string = sqlDatabaseName
