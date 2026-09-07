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

@description('Database SKU name, e.g. Basic (dev) or S1/GP_Gen5_2 (prod).')
param skuName string

@description('Database SKU tier, e.g. Basic or Standard.')
param skuTier string

@description('Principal id of the API managed identity, granted db_datareader/db_datawriter via AAD auth instead of a connection-string password.')
param apiPrincipalId string

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
  }

  resource allowAzureServices 'firewallRules@2023-08-01-preview' = {
    name: 'AllowAzureServices'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    // Dev can be thrown away; prod gets real geo-redundant backups.
    requestedBackupStorageRedundancy: environmentName == 'prod' ? 'Geo' : 'Local'
  }
}

@description('Fully qualified server hostname, for building the connection string.')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The database name.')
output databaseName string = sqlDatabaseName
