@description('Primary Azure region for data resources.')
param location string

@description('Short workload name used in resource naming.')
param workloadName string

@description('Deployment environment name (dev/staging/prod).')
param environmentName string

@description('Resource tags applied to all supported resources.')
param tags object = {}

@secure()
@description('SQL server administrator login password.')
param sqlAdminPassword string

@description('SQL server administrator login username.')
param sqlAdminLogin string = 'aonikadmin'

@description('SQL database SKU name.')
param sqlSkuName string = 'S0'

@description('SQL database max size in bytes.')
param sqlMaxSizeBytes int = 268435456000

var namePrefix = toLower('${workloadName}-${environmentName}')
var sqlServerHostnameSuffix = environment().suffixes.sqlServerHostname
var sqlServerFullyQualifiedDomainName = '${sqlServer.name}.${sqlServerHostnameSuffix}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${namePrefix}-sql'
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: 'aonik'
  parent: sqlServer
  location: location
  tags: tags
  sku: {
    name: sqlSkuName
  }
  properties: {
    maxSizeBytes: sqlMaxSizeBytes
    zoneRedundant: false
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${namePrefix}-kv'
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    sku: {
      family: 'A'
      name: 'standard'
    }
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'ConnectionStrings--DefaultConnection'
  parent: keyVault
  properties: {
    value: 'Server=tcp:${sqlServerFullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output keyVaultName string = keyVault.name
output keyVaultId string = keyVault.id
output sqlConnectionSecretUri string = sqlConnectionSecret.properties.secretUri
