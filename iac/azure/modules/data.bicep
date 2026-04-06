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

@secure()
@description('Azure Communication Services connection string. Stored in Key Vault even if empty so ACA secretRef entries always resolve.')
param acsConnectionString string = ''

@secure()
@description('HMAC hash key used by the verification service. Stored in Key Vault even if empty so ACA secretRef entries always resolve.')
param verificationHashKey string = ''

@description('SQL server administrator login username.')
param sqlAdminLogin string = 'aonikadmin'

@description('SQL database SKU name.')
param sqlSkuName string = 'S0'

@description('SQL database max size in bytes.')
param sqlMaxSizeBytes int = 268435456000

@description('Enable CanNotDelete resource locks (recommended for prod).')
param enableResourceLocks bool = false

@description('Log Analytics workspace resource ID for diagnostic settings. Leave empty to skip.')
param logAnalyticsWorkspaceId string = ''

@description('Public network access setting for SQL Server and Key Vault. Set to Disabled when using private endpoints.')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string = 'Enabled'

var namePrefix = toLower('${workloadName}-${environmentName}')
var sqlServerHostnameSuffix = environment().suffixes.sqlServerHostname
var sqlServerFullyQualifiedDomainName = '${sqlServer.name}${sqlServerHostnameSuffix}'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${namePrefix}-sql'
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    publicNetworkAccess: publicNetworkAccess
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
    publicNetworkAccess: publicNetworkAccess
  }
}

resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'ConnectionStrings--DefaultConnection'
  parent: keyVault
  properties: {
    value: 'Server=tcp:${sqlServerFullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource acsConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'Communication--Azure--ConnectionString'
  parent: keyVault
  properties: {
    value: acsConnectionString
  }
}

resource verificationHashKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'Verification--HashKey'
  parent: keyVault
  properties: {
    value: verificationHashKey
  }
}

// ── Resource Locks (production protection) ──────────────────────────

resource sqlServerLock 'Microsoft.Authorization/locks@2020-05-01' = if (enableResourceLocks) {
  name: '${sqlServer.name}-lock'
  scope: sqlServer
  properties: {
    level: 'CanNotDelete'
    notes: 'Prevent accidental deletion of SQL Server.'
  }
}

resource keyVaultLock 'Microsoft.Authorization/locks@2020-05-01' = if (enableResourceLocks) {
  name: '${keyVault.name}-lock'
  scope: keyVault
  properties: {
    level: 'CanNotDelete'
    notes: 'Prevent accidental deletion of Key Vault.'
  }
}

// ── Diagnostic Settings ─────────────────────────────────────────────

resource sqlServerDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (logAnalyticsWorkspaceId != '') {
  name: '${sqlServer.name}-diag'
  scope: sqlServer
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource sqlDatabaseDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (logAnalyticsWorkspaceId != '') {
  name: '${sqlDatabase.name}-diag'
  scope: sqlDatabase
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLSecurityAuditEvents'
        enabled: true
      }
      {
        category: 'SQLInsights'
        enabled: true
      }
      {
        category: 'Errors'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (logAnalyticsWorkspaceId != '') {
  name: '${keyVault.name}-diag'
  scope: keyVault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'audit'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// ── Qdrant Vector Store Storage ─────────────────────────────────────

@description('Qdrant API key for authentication.')
param qdrantApiKey string = 'dev-qdrant-key-changeme'

resource qdrantStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: replace('${namePrefix}qdrant', '-', '')
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource qdrantFileService 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  name: 'default'
  parent: qdrantStorageAccount
}

resource qdrantStorageShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  name: 'qdrant-storage'
  parent: qdrantFileService
  properties: {
    shareQuota: 100
    accessTier: 'Hot'
  }
}

resource qdrantSnapshotsShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  name: 'qdrant-snapshots'
  parent: qdrantFileService
  properties: {
    shareQuota: 50
    accessTier: 'Hot'
  }
}

resource qdrantApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'Qdrant--ApiKey'
  parent: keyVault
  properties: {
    value: qdrantApiKey
  }
}

resource qdrantStorageAccountKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'Qdrant--StorageAccountName'
  parent: keyVault
  properties: {
    value: qdrantStorageAccount.name
  }
}

resource qdrantStorageAccountKeyValueSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'Qdrant--StorageAccountKey'
  parent: keyVault
  properties: {
    value: qdrantStorageAccount.listKeys().keys[0].value
  }
}

output qdrantStorageAccountName string = qdrantStorageAccount.name
output qdrantStorageAccountKey string = qdrantStorageAccount.listKeys().keys[0].value
output qdrantApiKeySecretUri string = qdrantApiKeySecret.properties.secretUri
output qdrantStorageAccountNameSecretUri string = qdrantStorageAccountKeySecret.properties.secretUri
output qdrantStorageAccountKeySecretUri string = qdrantStorageAccountKeyValueSecret.properties.secretUri
output sqlServerName string = sqlServer.name
output sqlServerId string = sqlServer.id
output sqlDatabaseName string = sqlDatabase.name
output sqlDatabaseId string = sqlDatabase.id
output keyVaultName string = keyVault.name
output keyVaultId string = keyVault.id
output sqlConnectionSecretUri string = sqlConnectionSecret.properties.secretUri
output acsConnectionStringSecretUri string = acsConnectionStringSecret.properties.secretUri
output verificationHashKeySecretUri string = verificationHashKeySecret.properties.secretUri
