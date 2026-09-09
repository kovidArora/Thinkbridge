using '../main.bicep'

param environmentName = 'prod'
param location = 'centralindia'

param containerImage = 'crthinkschool.azurecr.io/quotes-api:latest'
param apiCpuCores = '1.0'
param apiMemorySize = '2Gi'
param apiMinReplicas = 1 // never scale to zero in prod — avoids a cold start on the first real request
param apiMaxReplicas = 5

// Same subscription-wide Container Apps Environment cap as dev — this trial
// only ever gets the one real environment, shared by both. A subscription
// without that cap would leave this at the default (true, separate envs).
param createNewApiEnvironment = false
param existingApiEnvironmentId = '/subscriptions/f9bb3282-0f51-41f0-b080-9ce9163bb32c/resourceGroups/rg-thinkschool-env/providers/Microsoft.App/managedEnvironments/cae-33kewg57w25su'
param containerRegistryId = '/subscriptions/f9bb3282-0f51-41f0-b080-9ce9163bb32c/resourceGroups/rg-thinkschool-env/providers/Microsoft.ContainerRegistry/registries/cr33kewg57w25su'

param sqlAdminLogin = 'quotesadmin'
// Read from an env var at deploy time, never stored in this file:
//   $env:SQL_ADMIN_PASSWORD = "..."; azd provision
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD')
// Same free-eligible serverless defaults as dev — the free offer covers up
// to 10 databases per subscription, so prod gets its own free grant too,
// separate from dev's.

param deployServiceBus = false

// Same pattern as sqlAdminPassword — read at deploy time, never stored here:
//   $env:INTERNAL_JWT_SIGNING_KEY = "..."; azd provision
param internalJwtSigningKey = readEnvironmentVariable('INTERNAL_JWT_SIGNING_KEY')

// Not secret — matches appsettings.json's Entra section exactly.
param entraTenantId = '74c0f8e6-65d8-427e-839e-aa716fee2987'
param entraAudience = 'api://bfea74ae-77f5-474a-bed6-a3de4d9ef099'
