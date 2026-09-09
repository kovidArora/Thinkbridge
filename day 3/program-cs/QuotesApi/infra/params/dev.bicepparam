using '../main.bicep'

param environmentName = 'dev'
param location = 'centralindia'

param containerImage = 'crthinkschool.azurecr.io/quotes-api:latest'
param apiCpuCores = '0.25'
param apiMemorySize = '0.5Gi'
param apiMinReplicas = 0 // scale-to-zero is fine for dev, saves cost when nobody's using it
param apiMaxReplicas = 1

// This subscription (free trial) caps Container Apps Environments at ONE,
// subscription-wide — reuse the existing real one instead of creating a
// second (see modules/api.bicep for the general parameterized escape hatch;
// most subscriptions would not need this and could leave the default true).
param createNewApiEnvironment = false
param existingApiEnvironmentId = '/subscriptions/f9bb3282-0f51-41f0-b080-9ce9163bb32c/resourceGroups/rg-thinkschool-env/providers/Microsoft.App/managedEnvironments/cae-33kewg57w25su'
param containerRegistryId = '/subscriptions/f9bb3282-0f51-41f0-b080-9ce9163bb32c/resourceGroups/rg-thinkschool-env/providers/Microsoft.ContainerRegistry/registries/cr33kewg57w25su'

param sqlAdminLogin = 'quotesadmin'
// Read from an env var at deploy time, never stored in this file:
//   $env:SQL_ADMIN_PASSWORD = "..."; azd provision
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
// sqlSkuName/skuTier/vCores/autoPauseDelayMinutes intentionally left at
// modules/sql.bicep's defaults (GP_S_Gen5 serverless, 1 vCore, useFreeLimit)
// so this database qualifies for the Azure SQL free offer.

// Service Bus stays undeployed — no free-tier option for topics.
param deployServiceBus = false
