@description('Primary Azure region.')
param location string

@description('Short workload name used in resource naming.')
param workloadName string

@description('Deployment environment name (dev/staging/prod).')
param environmentName string

@description('Resource tags applied to all supported resources.')
param tags object = {}

@description('VNet address space.')
param vnetAddressPrefix string = '10.0.0.0/16'

@description('Subnet for ACA infrastructure. Must be at least /23 for consumption-only environments.')
param acaSubnetAddressPrefix string = '10.0.0.0/23'

@description('Subnet for private endpoints.')
param privateEndpointsSubnetAddressPrefix string = '10.0.2.0/24'

@description('Resource ID of the SQL Server to create a private endpoint for.')
param sqlServerId string

@description('Resource ID of the Key Vault to create a private endpoint for.')
param keyVaultId string

var namePrefix = toLower('${workloadName}-${environmentName}')

// ── Virtual Network ─────────────────────────────────────────────────

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' = {
  name: '${namePrefix}-vnet'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'aca'
        properties: {
          addressPrefix: acaSubnetAddressPrefix
          delegations: [
            {
              name: 'aca-delegation'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: privateEndpointsSubnetAddressPrefix
        }
      }
    ]
  }
}

// ── Private Endpoints ───────────────────────────────────────────────

resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: '${namePrefix}-sql-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-sql-plsc'
        properties: {
          privateLinkServiceId: sqlServerId
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

resource kvPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = {
  name: '${namePrefix}-kv-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-kv-plsc'
        properties: {
          privateLinkServiceId: keyVaultId
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

// ── Private DNS Zones ───────────────────────────────────────────────

resource sqlDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink${environment().suffixes.sqlServerHostname}'
  location: 'global'
  tags: tags
}

resource kvDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
  tags: tags
}

// ── DNS Zone VNet Links ─────────────────────────────────────────────

resource sqlDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  name: '${namePrefix}-sql-vnet-link'
  parent: sqlDnsZone
  location: 'global'
  tags: tags
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

resource kvDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  name: '${namePrefix}-kv-vnet-link'
  parent: kvDnsZone
  location: 'global'
  tags: tags
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// ── DNS Zone Groups (auto-register PE IPs in DNS) ───────────────────

resource sqlDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  name: 'default'
  parent: sqlPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sqlServer'
        properties: {
          privateDnsZoneId: sqlDnsZone.id
        }
      }
    ]
  }
}

resource kvDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-01-01' = {
  name: 'default'
  parent: kvPrivateEndpoint
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'keyVault'
        properties: {
          privateDnsZoneId: kvDnsZone.id
        }
      }
    ]
  }
}

// ── Outputs ─────────────────────────────────────────────────────────

output vnetId string = vnet.id
output vnetName string = vnet.name
output acaSubnetId string = vnet.properties.subnets[0].id
output privateEndpointsSubnetId string = vnet.properties.subnets[1].id
