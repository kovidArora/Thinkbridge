using '../main.bicep'

param environmentName = 'prod'
param location = 'centralindia'

param containerImage = 'crthinkschool.azurecr.io/quotes-api:latest'
param apiCpuCores = '1.0'
param apiMemorySize = '2Gi'
param apiMinReplicas = 1 // never scale to zero in prod — avoids a cold start on the first real request
param apiMaxReplicas = 5

param sqlAdminLogin = 'quotesadmin'
// Read from an env var at deploy time, never stored in this file:
//   $env:SQL_ADMIN_PASSWORD = "..."; az deployment group create ...
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
param sqlSkuName = 'S1'
param sqlSkuTier = 'Standard'

param serviceBusSkuName = 'Standard'
