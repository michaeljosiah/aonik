targetScope = 'resourceGroup'

@description('Primary Azure region for this deployment.')
param location string = resourceGroup().location

@description('Short workload name used in resource naming.')
param workloadName string = 'aonik'

@description('Deployment environment name (dev/staging/prod).')
param environmentName string

@description('API image reference including tag.')
param apiImage string

@description('Admin UI image reference including tag.')
param adminUiImage string

@secure()
@description('SQL server administrator login password.')
param sqlAdminPassword string

@secure()
@description('Azure Communication Services connection string. Passed to Key Vault via the data module.')
param acsConnectionString string = ''

@secure()
@description('HMAC hash key for the verification service. Passed to Key Vault via the data module.')
param verificationHashKey string = ''

@description('Resource tags applied to all supported resources.')
param tags object = {}

@description('Optional API app settings overrides (name/value pairs).')
param apiAppSettings object = {}

@description('Azure Container Registry SKU tier.')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param containerRegistrySku string = 'Basic'

@description('Enable CanNotDelete resource locks on data resources (recommended for prod).')
param enableResourceLocks bool = false

@description('Enable private endpoints for SQL and Key Vault. App Service VNet integration is not included in this profile.')
param enableNetworkIsolation bool = false

module common '../../modules/common.bicep' = {
  name: 'common-${environmentName}'
  params: {
    location: location
    workloadName: workloadName
    environmentName: environmentName
    tags: tags
    containerRegistrySku: containerRegistrySku
  }
}

module data '../../modules/data.bicep' = {
  name: 'data-${environmentName}'
  params: {
    location: location
    workloadName: workloadName
    environmentName: environmentName
    tags: tags
    sqlAdminPassword: sqlAdminPassword
    acsConnectionString: acsConnectionString
    verificationHashKey: verificationHashKey
    enableResourceLocks: enableResourceLocks
    logAnalyticsWorkspaceId: common.outputs.logAnalyticsWorkspaceId
    publicNetworkAccess: enableNetworkIsolation ? 'Disabled' : 'Enabled'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${workloadName}-${environmentName}-asp'
  location: location
  tags: tags
  sku: {
    name: environmentName == 'prod' ? 'P1v3' : 'B1'
    tier: environmentName == 'prod' ? 'PremiumV3' : 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource apiWebApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${workloadName}-${environmentName}-api'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${apiImage}'
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(SecretUri=${data.outputs.sqlConnectionSecretUri})'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: common.outputs.appInsightsConnectionString
        }
        {
          name: 'Communication__Azure__ConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=${data.outputs.acsConnectionStringSecretUri})'
        }
        {
          name: 'Verification__HashKey'
          value: '@Microsoft.KeyVault(SecretUri=${data.outputs.verificationHashKeySecretUri})'
        }
      ] ++ [for setting in items(apiAppSettings): {
        name: setting.key
        value: string(setting.value)
      }]
      acrUseManagedIdentityCreds: true
    }
  }
}


resource adminUiWebApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${workloadName}-${environmentName}-adminui'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${adminUiImage}'
      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
      ]
      acrUseManagedIdentityCreds: true
    }
  }
}

resource acrPullRoleForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(common.outputs.containerRegistryId, apiWebApp.id, 'AcrPull')
  scope: resourceId('Microsoft.ContainerRegistry/registries', common.outputs.containerRegistryName)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: apiWebApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}


resource acrPullRoleForAdminUi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(common.outputs.containerRegistryId, adminUiWebApp.id, 'AcrPull')
  scope: resourceId('Microsoft.ContainerRegistry/registries', common.outputs.containerRegistryName)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: adminUiWebApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource kvSecretsUserRoleForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(data.outputs.keyVaultId, apiWebApp.id, 'KeyVaultSecretsUser')
  scope: resourceId('Microsoft.KeyVault/vaults', data.outputs.keyVaultName)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: apiWebApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiUrl string = 'https://${apiWebApp.properties.defaultHostName}'
output adminUiUrl string = 'https://${adminUiWebApp.properties.defaultHostName}'
output containerRegistryLoginServer string = common.outputs.containerRegistryLoginServer
output keyVaultName string = data.outputs.keyVaultName
output sqlServerName string = data.outputs.sqlServerName
