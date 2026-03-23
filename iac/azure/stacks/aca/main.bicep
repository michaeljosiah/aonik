targetScope = 'resourceGroup'

@description('Primary Azure region for this deployment.')
param location string = resourceGroup().location

@description('Short workload name used in resource naming.')
param workloadName string = 'aonik'

@description('Deployment environment name (dev/staging/prod).')
param environmentName string

@description('API image reference including tag.')
param apiImage string

@description('Worker image reference including tag.')
param workerImage string

@description('Admin UI image reference including tag.')
param adminUiImage string

@description('Admin UI container ingress target port.')
param adminUiTargetPort int = 80

@secure()
@description('SQL server administrator login password.')
param sqlAdminPassword string

@secure()
@description('Azure Communication Services connection string. Passed to Key Vault via the data module.')
param acsConnectionString string = ''

@secure()
@description('HMAC hash key for the verification service. Passed to Key Vault via the data module.')
param verificationHashKey string = ''

@secure()
@description('One-time platform bootstrap install code injected into the API container.')
param bootstrapSetupSecret string = ''

@description('Resource tags applied to all supported resources.')
param tags object = {}

@description('Optional API container environment variable overrides (name/value pairs).')
param apiAppSettings object = {}

@description('Optional worker container environment variable overrides (name/value pairs).')
param workerAppSettings object = {}

@description('Azure Container Registry SKU tier.')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param containerRegistrySku string = 'Basic'

@description('Enable CanNotDelete resource locks on data resources (recommended for prod).')
param enableResourceLocks bool = false

@description('Enable VNet integration with private endpoints for SQL and Key Vault (recommended for prod).')
param enableNetworkIsolation bool = false

var namePrefix = toLower('${workloadName}-${environmentName}')
var containerRegistryName = replace('${namePrefix}acr', '-', '')
var logAnalyticsWorkspaceName = '${namePrefix}-log'
var keyVaultName = '${namePrefix}-kv'
@description('Whether real ACS and verification secrets are provided (enables Key Vault references instead of empty inline values).')
param enableOptionalSecrets bool = false
var apiAdditionalEnvVars = [for setting in items(apiAppSettings): {
  name: setting.key
  value: string(setting.value)
}]
var workerAdditionalEnvVars = [for setting in items(workerAppSettings): {
  name: setting.key
  value: string(setting.value)
}]

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

module network '../../modules/network.bicep' = if (enableNetworkIsolation) {
  name: 'network-${environmentName}'
  params: {
    location: location
    workloadName: workloadName
    environmentName: environmentName
    tags: tags
    sqlServerId: data.outputs.sqlServerId
    keyVaultId: data.outputs.keyVaultId
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${workloadName}-${environmentName}-acae'
  location: location
  tags: tags
  properties: {
    vnetConfiguration: enableNetworkIsolation ? {
      infrastructureSubnetId: network.outputs.acaSubnetId
      internal: false
    } : null
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

resource apiPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${workloadName}-${environmentName}-api-pull-id'
  location: location
  tags: tags
}

resource workerPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${workloadName}-${environmentName}-worker-pull-id'
  location: location
  tags: tags
}

resource adminUiPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${workloadName}-${environmentName}-adminui-pull-id'
  location: location
  tags: tags
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${workloadName}-${environmentName}-api'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${apiPullIdentity.id}': {}
    }
  }
  dependsOn: [
    acrPullRoleForApiPullIdentity
  ]
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: common.outputs.containerRegistryLoginServer
          identity: apiPullIdentity.id
        }
      ]
      secrets: concat(enableOptionalSecrets ? [
        {
          name: 'sql-connection'
          keyVaultUrl: data.outputs.sqlConnectionSecretUri
          identity: 'system'
        }
        {
          name: 'app-insights-connection-string'
          value: common.outputs.appInsightsConnectionString
        }
        {
          name: 'acs-connection-string'
          keyVaultUrl: data.outputs.acsConnectionStringSecretUri
          identity: 'system'
        }
        {
          name: 'verification-hash-key'
          keyVaultUrl: data.outputs.verificationHashKeySecretUri
          identity: 'system'
        }
      ] : [
        {
          name: 'sql-connection'
          keyVaultUrl: data.outputs.sqlConnectionSecretUri
          identity: 'system'
        }
        {
          name: 'app-insights-connection-string'
          value: common.outputs.appInsightsConnectionString
        }
        {
          name: 'acs-connection-string'
          value: 'placeholder'
        }
        {
          name: 'verification-hash-key'
          value: 'placeholder'
        }
      ], empty(bootstrapSetupSecret) ? [] : [
        {
          name: 'bootstrap-setup-secret'
          value: bootstrapSetupSecret
        }
      ])
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          env: concat([
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environmentName
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'app-insights-connection-string'
            }
            {
              name: 'Communication__Azure__ConnectionString'
              secretRef: 'acs-connection-string'
            }
            {
              name: 'Verification__HashKey'
              secretRef: 'verification-hash-key'
            }
          ], empty(bootstrapSetupSecret) ? [] : [
            {
              name: 'Bootstrap__SetupSecret'
              secretRef: 'bootstrap-setup-secret'
            }
          ], [
            {
              name: 'Cors__AllowedOrigins__0'
              value: 'https://${adminUiApp.properties.configuration.ingress.fqdn}'
            }
          ], apiAdditionalEnvVars)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: environmentName == 'prod' ? 2 : 1
        maxReplicas: environmentName == 'prod' ? 10 : 3
      }
    }
  }
}

resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${workloadName}-${environmentName}-worker'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${workerPullIdentity.id}': {}
    }
  }
  dependsOn: [
    acrPullRoleForWorkerPullIdentity
  ]
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: common.outputs.containerRegistryLoginServer
          identity: workerPullIdentity.id
        }
      ]
      secrets: [
        {
          name: 'sql-connection'
          keyVaultUrl: data.outputs.sqlConnectionSecretUri
          identity: 'system'
        }
        {
          name: 'app-insights-connection-string'
          value: common.outputs.appInsightsConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'worker'
          image: workerImage
          env: concat([
            {
              name: 'DOTNET_ENVIRONMENT'
              value: environmentName
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'sql-connection'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'app-insights-connection-string'
            }
          ], workerAdditionalEnvVars)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: environmentName == 'prod' ? 5 : 2
      }
    }
  }
}

resource adminUiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${workloadName}-${environmentName}-adminui'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${adminUiPullIdentity.id}': {}
    }
  }
  dependsOn: [
    acrPullRoleForAdminUiPullIdentity
  ]
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: adminUiTargetPort
        transport: 'auto'
      }
      registries: [
        {
          server: common.outputs.containerRegistryLoginServer
          identity: adminUiPullIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'adminui'
          image: adminUiImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: environmentName == 'prod' ? 2 : 1
        maxReplicas: environmentName == 'prod' ? 5 : 2
      }
    }
  }
}


resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: containerRegistryName
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource acrPullRoleForApiPullIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistryName, apiPullIdentity.name, 'AcrPull')
  scope: containerRegistry
  dependsOn: [
    common
  ]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: apiPullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullRoleForWorkerPullIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistryName, workerPullIdentity.name, 'AcrPull')
  scope: containerRegistry
  dependsOn: [
    common
  ]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: workerPullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullRoleForAdminUiPullIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistryName, adminUiPullIdentity.name, 'AcrPull')
  scope: containerRegistry
  dependsOn: [
    common
  ]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: adminUiPullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource kvSecretsUserRoleForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultName, apiApp.name, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource kvSecretsUserRoleForWorker 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultName, workerApp.name, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: workerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output adminUiUrl string = 'https://${adminUiApp.properties.configuration.ingress.fqdn}'
output containerRegistryLoginServer string = common.outputs.containerRegistryLoginServer
output keyVaultName string = data.outputs.keyVaultName
output sqlServerName string = data.outputs.sqlServerName
