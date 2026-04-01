@description('Primary Azure region for monitoring resources.')
param location string

@description('Short workload name used in resource naming.')
param workloadName string

@description('Deployment environment name (dev/staging/prod).')
param environmentName string

@description('Resource tags applied to all supported resources.')
param tags object = {}

@description('Enable Azure Monitor alerts and webhook delivery.')
param alertsEnabled bool = false

@secure()
@description('Webhook service URI for Azure Monitor action groups. This can include a shared secret query string.')
param alertsWebhookServiceUri string

@description('Log Analytics workspace resource ID used by scheduled query alerts.')
param logAnalyticsWorkspaceId string

@description('API Container App resource ID.')
param apiAppResourceId string

@description('API Container App name.')
param apiAppName string

@description('Worker Container App resource ID.')
param workerAppResourceId string

@description('Worker Container App name.')
param workerAppName string

@description('SQL database resource ID.')
param sqlDatabaseResourceId string

@description('Key Vault resource ID.')
param keyVaultResourceId string

var namePrefix = toLower('${workloadName}-${environmentName}')
var actionGroupName = '${namePrefix}-ops-ag'

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (alertsEnabled) {
  name: actionGroupName
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'aonikops'
    enabled: true
    webhookReceivers: [
      {
        name: 'aonik-platform-alerts'
        serviceUri: alertsWebhookServiceUri
        useCommonAlertSchema: true
      }
    ]
  }
}

resource apiFiveXxAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (alertsEnabled) {
  name: '${namePrefix}-api-5xx'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    enabled: true
    description: 'Detects elevated API 5xx responses in Application Insights / Log Analytics telemetry.'
    displayName: '${environmentName} API 5xx spike'
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 1
    autoMitigate: true
    resolveConfiguration: {
      autoResolved: true
      timeToResolve: 'PT15M'
    }
    skipQueryValidation: true
    scopes: [
      logAnalyticsWorkspaceId
    ]
    criteria: {
      allOf: [
        {
          query: '''
union isfuzzy=true
  (AppRequests | project EventTime = TimeGenerated, StatusCode = tostring(ResultCode), ResourceId = _ResourceId, RoleName = tostring(AppRoleName)),
  (requests | project EventTime = timestamp, StatusCode = tostring(resultCode), ResourceId = _ResourceId, RoleName = tostring(cloud_RoleName))
| where EventTime > ago(5m)
| where ResourceId =~ '${apiAppResourceId}' or RoleName =~ '${apiAppName}'
| where StatusCode startswith '5'
| summarize FailureCount = count()
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
      customProperties: {
        alertCategory: 'performance'
        environmentName: environmentName
        componentName: apiAppName
        resourceId: apiAppResourceId
      }
    }
  }
}

resource apiLatencyAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (alertsEnabled) {
  name: '${namePrefix}-api-latency'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    enabled: true
    description: 'Detects sustained slow API requests.'
    displayName: '${environmentName} API latency spike'
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 2
    autoMitigate: true
    resolveConfiguration: {
      autoResolved: true
      timeToResolve: 'PT15M'
    }
    skipQueryValidation: true
    scopes: [
      logAnalyticsWorkspaceId
    ]
    criteria: {
      allOf: [
        {
          query: '''
union isfuzzy=true
  (AppRequests | project EventTime = TimeGenerated, DurationMs = todouble(DurationMs), ResourceId = _ResourceId, RoleName = tostring(AppRoleName)),
  (requests | project EventTime = timestamp, DurationMs = todouble(duration / 1ms), ResourceId = _ResourceId, RoleName = tostring(cloud_RoleName))
| where EventTime > ago(5m)
| where ResourceId =~ '${apiAppResourceId}' or RoleName =~ '${apiAppName}'
| where DurationMs > 2000
| summarize SlowRequestCount = count()
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 10
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
      customProperties: {
        alertCategory: 'performance'
        environmentName: environmentName
        componentName: apiAppName
        resourceId: apiAppResourceId
      }
    }
  }
}

resource apiExceptionAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (alertsEnabled) {
  name: '${namePrefix}-api-exceptions'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    enabled: true
    description: 'Detects elevated exception counts for the API container app.'
    displayName: '${environmentName} API exception spike'
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 1
    autoMitigate: true
    resolveConfiguration: {
      autoResolved: true
      timeToResolve: 'PT15M'
    }
    skipQueryValidation: true
    scopes: [
      logAnalyticsWorkspaceId
    ]
    criteria: {
      allOf: [
        {
          query: '''
union isfuzzy=true
  (AppExceptions | project EventTime = TimeGenerated, ResourceId = _ResourceId, RoleName = tostring(AppRoleName)),
  (exceptions | project EventTime = timestamp, ResourceId = _ResourceId, RoleName = tostring(cloud_RoleName))
| where EventTime > ago(5m)
| where ResourceId =~ '${apiAppResourceId}' or RoleName =~ '${apiAppName}'
| summarize ExceptionCount = count()
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
      customProperties: {
        alertCategory: 'performance'
        environmentName: environmentName
        componentName: apiAppName
        resourceId: apiAppResourceId
      }
    }
  }
}

resource workerExceptionAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (alertsEnabled) {
  name: '${namePrefix}-worker-exceptions'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    enabled: true
    description: 'Detects elevated exception counts for the worker container app.'
    displayName: '${environmentName} worker exception spike'
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 1
    autoMitigate: true
    resolveConfiguration: {
      autoResolved: true
      timeToResolve: 'PT15M'
    }
    skipQueryValidation: true
    scopes: [
      logAnalyticsWorkspaceId
    ]
    criteria: {
      allOf: [
        {
          query: '''
union isfuzzy=true
  (AppExceptions | project EventTime = TimeGenerated, ResourceId = _ResourceId, RoleName = tostring(AppRoleName)),
  (exceptions | project EventTime = timestamp, ResourceId = _ResourceId, RoleName = tostring(cloud_RoleName))
| where EventTime > ago(5m)
| where ResourceId =~ '${workerAppResourceId}' or RoleName =~ '${workerAppName}'
| summarize ExceptionCount = count()
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 3
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
      customProperties: {
        alertCategory: 'operations'
        environmentName: environmentName
        componentName: workerAppName
        resourceId: workerAppResourceId
      }
    }
  }
}

resource dependencyFailureAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (alertsEnabled) {
  name: '${namePrefix}-dependency-failures'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    enabled: true
    description: 'Detects spikes in failed downstream dependencies from the API.'
    displayName: '${environmentName} dependency failure spike'
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 2
    autoMitigate: true
    resolveConfiguration: {
      autoResolved: true
      timeToResolve: 'PT15M'
    }
    skipQueryValidation: true
    scopes: [
      logAnalyticsWorkspaceId
    ]
    criteria: {
      allOf: [
        {
          query: '''
union isfuzzy=true
  (AppDependencies | project EventTime = TimeGenerated, SuccessValue = tobool(Success), ResourceId = _ResourceId, RoleName = tostring(AppRoleName)),
  (dependencies | project EventTime = timestamp, SuccessValue = tobool(success), ResourceId = _ResourceId, RoleName = tostring(cloud_RoleName))
| where EventTime > ago(5m)
| where ResourceId =~ '${apiAppResourceId}' or RoleName =~ '${apiAppName}'
| where SuccessValue == false
| summarize FailureCount = count()
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
      customProperties: {
        alertCategory: 'operations'
        environmentName: environmentName
        componentName: apiAppName
        resourceId: apiAppResourceId
      }
    }
  }
}

resource keyVaultFailureAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (alertsEnabled) {
  name: '${namePrefix}-keyvault-failures'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    enabled: true
    description: 'Detects failed Azure Key Vault operations captured in diagnostics logs.'
    displayName: '${environmentName} Key Vault failure spike'
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    severity: 1
    autoMitigate: true
    resolveConfiguration: {
      autoResolved: true
      timeToResolve: 'PT15M'
    }
    skipQueryValidation: true
    scopes: [
      logAnalyticsWorkspaceId
    ]
    criteria: {
      allOf: [
        {
          query: '''
AzureDiagnostics
| where TimeGenerated > ago(5m)
| where ResourceId =~ '${keyVaultResourceId}'
| where ResultType != 'Success'
| summarize FailureCount = count()
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            minFailingPeriodsToAlert: 1
            numberOfEvaluationPeriods: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
      customProperties: {
        alertCategory: 'security'
        environmentName: environmentName
        componentName: 'key-vault'
        resourceId: keyVaultResourceId
      }
    }
  }
}

resource sqlCpuAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = if (alertsEnabled) {
  name: '${namePrefix}-sql-cpu'
  location: 'global'
  tags: tags
  properties: {
    description: 'Detects sustained high SQL CPU usage.'
    enabled: true
    autoMitigate: true
    severity: 2
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    scopes: [
      sqlDatabaseResourceId
    ]
    targetResourceType: 'Microsoft.Sql/servers/databases'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'sql-cpu-percent'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'cpu_percent'
          timeAggregation: 'Average'
          operator: 'GreaterThan'
          threshold: 80
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
        webHookProperties: {
          alertCategory: 'performance'
          environmentName: environmentName
          componentName: 'sql-database'
          resourceId: sqlDatabaseResourceId
        }
      }
    ]
    customProperties: {
      alertCategory: 'performance'
      environmentName: environmentName
      componentName: 'sql-database'
      resourceId: sqlDatabaseResourceId
    }
  }
}

resource sqlStorageAlert 'Microsoft.Insights/metricAlerts@2024-03-01-preview' = if (alertsEnabled) {
  name: '${namePrefix}-sql-storage'
  location: 'global'
  tags: tags
  properties: {
    description: 'Detects when SQL storage consumption approaches configured limits.'
    enabled: true
    autoMitigate: true
    severity: 1
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    scopes: [
      sqlDatabaseResourceId
    ]
    targetResourceType: 'Microsoft.Sql/servers/databases'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'sql-storage-percent'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'storage_percent'
          timeAggregation: 'Average'
          operator: 'GreaterThan'
          threshold: 85
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
        webHookProperties: {
          alertCategory: 'performance'
          environmentName: environmentName
          componentName: 'sql-database'
          resourceId: sqlDatabaseResourceId
        }
      }
    ]
    customProperties: {
      alertCategory: 'performance'
      environmentName: environmentName
      componentName: 'sql-database'
      resourceId: sqlDatabaseResourceId
    }
  }
}

output actionGroupId string = alertsEnabled ? actionGroup.id : ''
