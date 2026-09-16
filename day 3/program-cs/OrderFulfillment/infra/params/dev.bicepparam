using '../main.bicep'

param environmentName = 'dev'
param location = 'centralindia'

param containerImage = 'crthinkschool.azurecr.io/orderfulfillment-api:latest'
param apiCpuCores = '0.25'
param apiMemorySize = '0.5Gi'
param apiMinReplicas = 0 // scale-to-zero is fine for dev
param apiMaxReplicas = 1

// Same subscription-wide Container Apps Environment cap already hit by
// QuotesApi — reuse the existing shared environment instead of creating a
// second one.
param createNewApiEnvironment = false
param existingApiEnvironmentId = '/subscriptions/f9bb3282-0f51-41f0-b080-9ce9163bb32c/resourceGroups/rg-thinkschool-env/providers/Microsoft.App/managedEnvironments/cae-33kewg57w25su'
param containerRegistryId = '/subscriptions/f9bb3282-0f51-41f0-b080-9ce9163bb32c/resourceGroups/rg-thinkschool-env/providers/Microsoft.ContainerRegistry/registries/cr33kewg57w25su'

param sqlAdminLogin = 'orderfulfillmentadmin'
// Read from an env var at deploy time, never stored here:
//   $env:SQL_ADMIN_PASSWORD = "..."; az deployment group create ...
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
