@description('Azure region for the private publishing file shares.')
param location string

param workloadName string
param environmentName string
param containerAppsEnvironmentName string
param tags object = {}

@description('Private intake quota, including retained archives, uploaded parts and assembly headroom.')
@minValue(10)
@maxValue(1024)
param intakeShareQuotaGiB int = 10

var accountName = 'ankpub${uniqueString(resourceGroup().id, workloadName, environmentName)}'

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true // ACA's SMB mounts require the account key.
    accessTier: 'Hot'
    encryption: {
      keySource: 'Microsoft.Storage'
      requireInfrastructureEncryption: true
      services: { file: { enabled: true, keyType: 'Account' } }
    }
  }
}

resource files 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: { shareDeleteRetentionPolicy: { enabled: true, days: 7 } }
}

// Both shares are private. Only AONIK's committed-artifact route serves public bytes.
resource shares 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = [for name in ['intake', 'delivery']: {
  parent: files
  name: name
  properties: { shareQuota: name == 'intake' ? intakeShareQuotaGiB : 10, enabledProtocols: 'SMB' }
}]

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

resource mounts 'Microsoft.App/managedEnvironments/storages@2024-03-01' = [for (name, i) in ['intake', 'delivery']: {
  parent: environment
  name: 'publishing-${name}'
  properties: {
    azureFile: {
      accountName: account.name
      accountKey: account.listKeys().keys[0].value
      shareName: shares[i].name
      accessMode: 'ReadWrite'
    }
  }
}]

output accountName string = account.name
output intakeMountName string = mounts[0].name
output deliveryMountName string = mounts[1].name
