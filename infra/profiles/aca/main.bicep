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

@secure()
@description('SQL server administrator login password.')
param sqlAdminPassword string

@description('Resource tags applied to all supported resources.')
param tags object = {}

var namePrefix = toLower('${workloadName}-${environmentName}')
var containerRegistryName = replace('${namePrefix}acr', '-', '')
var logAnalyticsWorkspaceName = '${namePrefix}-log'
var keyVaultName = '${namePrefix}-kv'

module common '../../modules/common.bicep' = {
  name: 'common-${environmentName}'
  params: {
    location: location
    workloadName: workloadName
    environmentName: environmentName
    tags: tags
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
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${workloadName}-${environmentName}-acae'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: listKeys(logAnalyticsWorkspace.id, '2023-09-01').primarySharedKey
      }
    }
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${workloadName}-${environmentName}-api'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
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
          identity: 'system'
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
          name: 'api'
          image: apiImage
          env: [
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
          ]
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
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: common.outputs.containerRegistryLoginServer
          identity: 'system'
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
          env: [
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
          ]
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
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 80
        transport: 'auto'
      }
      registries: [
        {
          server: common.outputs.containerRegistryLoginServer
          identity: 'system'
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

resource acrPullRoleForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistryName, apiApp.name, 'AcrPull')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullRoleForWorker 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistryName, workerApp.name, 'AcrPull')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: workerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullRoleForAdminUi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistryName, adminUiApp.name, 'AcrPull')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: adminUiApp.identity.principalId
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
