// Network module: the VNet, subnet, and private DNS zone the SQL private
// endpoint lives in. Kept separate from sql.bicep since a real subscription
// would typically share one VNet across many resources' private endpoints,
// not build a fresh one per data store.

@description('Environment name, used in resource naming.')
@allowed(['dev', 'prod'])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

var vnetName = 'vnet-quotes-${environmentName}'
var privateEndpointSubnetName = 'snet-private-endpoints'
// This exact zone name is an Azure-defined constant for Azure SQL private
// endpoints (it differs on sovereign clouds like Azure Government/China,
// which environment() could express, but that's out of scope here).
#disable-next-line no-hardcoded-env-urls
var privateDnsZoneName = 'privatelink.database.windows.net'

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.20.0.0/16']
    }
    subnets: [
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: '10.20.1.0/24'
          // Required for a private endpoint to actually land in this
          // subnet — Azure blocks it otherwise (PrivateEndpointNetworkPoliciesCannotBeDisabled-
          // adjacent check on the reverse: policies must be Disabled to allow one at all).
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
      {
        // Kept separate from the private-endpoint subnet on purpose —
        // Azure Container Instances require their own delegated subnet,
        // and mixing delegation with a private-endpoint subnet is not
        // supported. This is only used for the short-lived connectivity
        // check, not for anything that stays deployed.
        name: 'snet-connectivity-check'
        properties: {
          addressPrefix: '10.20.2.0/24'
          delegations: [
            {
              name: 'aci-delegation'
              properties: {
                serviceName: 'Microsoft.ContainerInstance/containerGroups'
              }
            }
          ]
        }
      }
    ]
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: privateDnsZoneName
  location: 'global'
}

resource privateDnsZoneVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: privateDnsZone
  name: 'link-${vnetName}'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

@description('VNet resource id.')
output vnetId string = vnet.id

@description('Private endpoint subnet resource id.')
output privateEndpointSubnetId string = vnet.properties.subnets[0].id

@description('Connectivity-check subnet resource id (delegated to Container Instances).')
output connectivityCheckSubnetId string = vnet.properties.subnets[1].id

@description('Private DNS zone resource id, for the private endpoint DNS zone group.')
output privateDnsZoneId string = privateDnsZone.id
