@description('Primary Azure region for shared resources.')
param location string

@description('Short workload name used in resource naming.')
param workloadName string

@description('Deployment environment name (dev/staging/prod).')
param environmentName string

@description('Resource tags applied to all supported resources.')
param tags object = {}

@description('Whether to create an Azure Container Registry.')
param createContainerRegistry bool = true

@description('Whether to create an Azure Log Analytics workspace.')
param createLogAnalytics bool = true

@description('Whether to create Application Insights.')
param createApplicationInsights bool = true

var namePrefix = toLower('${workloadName}-${environmentName}')

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = if (createLogAnalytics) {
  name: '${namePrefix}-log'
  location: location
  tags: tags
  properties: {
    retentionInDays: 30
    features: {
      searchVersion: 1
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = if (createApplicationInsights) {
  name: '${namePrefix}-appi'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: createLogAnalytics ? logAnalytics.id : null
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = if (createContainerRegistry) {
  name: replace('${namePrefix}acr', '-', '')
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    policies: {
      quarantinePolicy: {
        status: 'disabled'
      }
      trustPolicy: {
        type: 'Notary'
        status: 'disabled'
      }
      retentionPolicy: {
        days: 14
        status: 'enabled'
      }
      exportPolicy: {
        status: 'enabled'
      }
      azureAdAuthenticationAsArmPolicy: {
        status: 'enabled'
      }
    }
  }
}

output containerRegistryName string = createContainerRegistry ? acr.name : ''
output containerRegistryId string = createContainerRegistry ? acr.id : ''
output containerRegistryLoginServer string = createContainerRegistry ? acr.properties.loginServer : ''
output logAnalyticsWorkspaceName string = createLogAnalytics ? logAnalytics.name : ''
output logAnalyticsWorkspaceId string = createLogAnalytics ? logAnalytics.id : ''
output logAnalyticsWorkspaceCustomerId string = createLogAnalytics ? logAnalytics.properties.customerId : ''
output appInsightsConnectionString string = createApplicationInsights ? appInsights.properties.ConnectionString : ''
