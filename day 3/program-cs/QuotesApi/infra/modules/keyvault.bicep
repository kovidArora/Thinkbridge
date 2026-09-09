// Key Vault module: holds the one real secret the app still needs at
// runtime — the internal-auth JWT signing key. SQL and Service Bus need no
// secret at all (Managed Identity handles both, see modules/api.bicep,
// modules/sql.bicep, modules/servicebus.bicep); Entra ID app auth needs no
// shared secret either (it validates tokens against a public JWKS, only
// TenantId/Audience, which are not secrets). RBAC authorization is used
// instead of vault access policies, matching the Managed Identity model used
// everywhere else in this stack.

@description('Environment name, used in vault naming.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region for the vault.')
param location string = resourceGroup().location

@secure()
@description('Signing key for the "Internal" JWT auth scheme (Program.cs) — the one app secret with no Managed Identity equivalent. Passed at deploy time only (CLI/pipeline env var), never stored in a params file.')
param internalJwtSigningKey string

// Key Vault names cap at 24 characters, tighter than every other resource in
// this stack — "kv-quotes-<env>-<uniqueString>" runs over that, so this uses
// a shorter prefix and a truncated uniqueString instead.
var keyVaultName = take('kv-${environmentName}-${uniqueString(resourceGroup().id)}', 24)

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    // RBAC, not access policies — role assignments are granted in
    // modules/api.bicep once the reader identity exists, the same pattern
    // used for the container registry and Service Bus.
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

resource jwtSigningKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'internal-jwt-signing-key'
  properties: {
    value: internalJwtSigningKey
  }
}

@description('Vault resource id, so callers can grant roles against it as an `existing` resource.')
output vaultId string = keyVault.id

@description('Vault URI, used to build Container App Key Vault secret references (vaultUri + "secrets/<name>").')
output vaultUri string = keyVault.properties.vaultUri
