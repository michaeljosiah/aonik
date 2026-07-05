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
@description('HMAC signing key for CodeAct callback nonces. Stored in Key Vault and surfaced to the API container as AI__CODEACT__NONCESIGNINGKEY. Leave empty in environments that disable the AcaSessions provider.')
param codeActNonceSigningKey string = ''

@secure()
@description('One-time platform bootstrap install code injected into the API container.')
param bootstrapSetupSecret string = ''

@description('Enable Azure Monitor alerts and webhook-based platform alert ingestion.')
param alertsEnabled bool = false

@secure()
@description('Shared secret used by Azure Monitor alert delivery to AONIK.')
param alertsSharedSecret string = ''

@description('Resource tags applied to all supported resources.')
param tags object = {}

@description('Optional API container environment variable overrides (name/value pairs).')
param apiAppSettings object = {}

@description('Optional worker container environment variable overrides (name/value pairs).')
param workerAppSettings object = {}

@description('Qdrant vector store image reference including tag.')
param qdrantImage string = 'qdrant/qdrant:latest'

@secure()
@description('Qdrant API key for authentication. Defaults to dev key.')
param qdrantApiKey string = 'dev-qdrant-key-changeme'

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
var apiAppName = '${namePrefix}-api'
var adminUiAppName = '${namePrefix}-adminui'
@description('Whether real ACS and verification secrets are provided (enables Key Vault references instead of empty inline values).')
param enableOptionalSecrets bool = false
var alertsWebhookServiceUri = 'https://${apiApp.properties.configuration.ingress.fqdn}/integrations/azure/alerts?code=${uriComponent(alertsSharedSecret)}'

// =============================================================================
// CodeAct / ACA Dynamic Sessions URL composition
// -----------------------------------------------------------------------------
// The session pool's management endpoint follows a deterministic pattern
// (https://learn.microsoft.com/en-us/azure/container-apps/sessions-code-interpreter),
// so we can compute it without referencing `sessions.outputs.*` in the apiApp
// env block — avoids the cycle "apiApp env → sessions → apiApp.identity".
// The callback URL mirrors the working pattern used for `Cors__AllowedOrigins`
// (line below) so we don't reference `apiApp.properties.configuration.ingress.fqdn`
// from inside apiApp's own env block (ARM rejects that self-reference).
// =============================================================================
var codeActSessionPoolName = '${namePrefix}-sessions'
var codeActPoolManagementEndpoint = 'https://${location}.dynamicsessions.io/subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroup().name}/sessionPools/${codeActSessionPoolName}'
var codeActCallbackBaseUrl = 'https://${apiAppName}.${containerAppsEnvironment.properties.defaultDomain}'
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
    codeActNonceSigningKey: codeActNonceSigningKey
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
      infrastructureSubnetId: network!.outputs.acaSubnetId
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

// ── Qdrant Storage Mounts for Managed Environment ──────────────────

resource qdrantStorageMount 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: 'qdrant-storage'
  parent: containerAppsEnvironment
  properties: {
    azureFile: {
      accountName: data.outputs.qdrantStorageAccountName
      accountKey: data.outputs.qdrantStorageAccountKey
      shareName: 'qdrant-storage'
      accessMode: 'ReadWrite'
    }
  }
}

resource qdrantSnapshotsMount 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: 'qdrant-snapshots'
  parent: containerAppsEnvironment
  properties: {
    azureFile: {
      accountName: data.outputs.qdrantStorageAccountName
      accountKey: data.outputs.qdrantStorageAccountKey
      shareName: 'qdrant-snapshots'
      accessMode: 'ReadWrite'
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

resource qdrantPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${workloadName}-${environmentName}-qdrant-pull-id'
  location: location
  tags: tags
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: apiAppName
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
        // 'http' (HTTP/1.1 with Transfer-Encoding: chunked) is required for
        // Server-Sent Events streaming (/ai/agui). Under 'auto' the ACA Envoy
        // proxy can negotiate HTTP/2 with the browser and buffer SSE frames,
        // causing 20-30s gateway-side delays even though the server flushes
        // per token. Pinning to HTTP/1.1 keeps chunked SSE unbuffered.
        transport: 'http'
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
      ], [
        // CodeAct nonce signing key — always a Key Vault reference regardless
        // of `enableOptionalSecrets`. The data module always creates the
        // secret (with empty value when no GH secret is configured); the
        // .NET-side validator throws a clear "must decode to at least 32
        // bytes" error when the KV value is empty. Previously this entry
        // sat inside the enableOptionalSecrets ternary with a 'placeholder'
        // literal fallback that ACA read in preference to the operator's
        // real secret — caused "received 11 characters after trimming"
        // when AcaSessions provider was enabled in dev.
        {
          name: 'code-act-nonce-signing-key'
          keyVaultUrl: data.outputs.codeActNonceSigningKeySecretUri
          identity: 'system'
        }
      ], empty(bootstrapSetupSecret) ? [] : [
        {
          name: 'bootstrap-setup-secret'
          value: bootstrapSetupSecret
        }
      ], [
        {
          name: 'operations-alerts-azure-monitor-shared-secret'
          value: empty(alertsSharedSecret) ? 'placeholder' : alertsSharedSecret
        }
      ], [
        {
          name: 'qdrant-api-key'
          value: qdrantApiKey
        }
      ], [
        {
          name: 'blob-storage-account-name'
          keyVaultUrl: data.outputs.blobStorageAccountNameSecretUri
          identity: 'system'
        }
        {
          name: 'blob-storage-account-key'
          keyVaultUrl: data.outputs.blobStorageAccountKeySecretUri
          identity: 'system'
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
              name: 'Database__AutoMigrate'
              value: 'true'
            }
            {
              name: 'Database__SeedData'
              value: 'true'
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
              name: 'Runtime__AzureContainerApps__Enabled'
              value: 'true'
            }
            {
              name: 'Runtime__AzureContainerApps__EnvironmentName'
              value: environmentName
            }
            {
              name: 'Runtime__AzureContainerApps__WorkloadName'
              value: workloadName
            }
            {
              name: 'Runtime__AzureContainerApps__SubscriptionId'
              value: subscription().subscriptionId
            }
            {
              name: 'Runtime__AzureContainerApps__ResourceGroupName'
              value: resourceGroup().name
            }
            {
              name: 'Communication__Azure__ConnectionString'
              secretRef: 'acs-connection-string'
            }
            {
              name: 'Verification__HashKey'
              secretRef: 'verification-hash-key'
            }
            {
              name: 'Operations__Alerts__AzureMonitor__SharedSecret'
              secretRef: 'operations-alerts-azure-monitor-shared-secret'
            }
          ], empty(bootstrapSetupSecret) ? [] : [
            {
              name: 'Bootstrap__Enabled'
              value: 'true'
            }
            {
              name: 'Bootstrap__SetupSecret'
              secretRef: 'bootstrap-setup-secret'
            }
          ], [
            {
              name: 'Cors__AllowedOrigins__0'
              value: 'https://${adminUiAppName}.${containerAppsEnvironment.properties.defaultDomain}'
            }
            {
              // This environment is the default backend for the packaged Aonik Admin
              // desktop (Electron), whose file:// renderer sends `Origin: null`. The
              // API keeps null-origin OFF by default (M12 / #124) as a hardening; a
              // deployment that serves the desktop opts back in explicitly here.
              name: 'Cors__AllowDesktopNullOrigin'
              value: 'true'
            }
            {
              name: 'Qdrant__Endpoint'
              value: 'http://${qdrantApp.name}'
            }
            {
              name: 'Qdrant__ApiKey'
              secretRef: 'qdrant-api-key'
            }
            {
              name: 'Qdrant__CollectionPrefix'
              value: 'aonik-${environmentName}'
            }
          ], [
            {
              name: 'BlobStorage__Provider'
              value: 'Azure'
            }
            {
              name: 'BlobStorage__Azure__AccountName'
              secretRef: 'blob-storage-account-name'
            }
            {
              name: 'BlobStorage__Azure__AccountKey'
              secretRef: 'blob-storage-account-key'
            }
            {
              name: 'BlobStorage__ProfilePhotos__PublicBaseUrl'
              value: '${data.outputs.blobStoragePublicEndpoint}profiles'
            }
            {
              name: 'BlobStorage__ContentMedia__PublicBaseUrl'
              value: '${data.outputs.blobStoragePublicEndpoint}content-media'
            }
          ], [
            // CodeAct / ACA Dynamic Sessions — Spec 025 sub-agent sandbox.
            // The provider is `Disabled` by default so this block is inert
            // until the operator opts in via cd-deploy.yml's AI__CODEACT__PROVIDER var.
            {
              name: 'AI__CODEACT__ACASESSIONS__POOLMANAGEMENTENDPOINT'
              value: codeActPoolManagementEndpoint
            }
            {
              name: 'AI__CODEACT__ACASESSIONS__CALLBACKBASEURL'
              value: codeActCallbackBaseUrl
            }
            {
              // Pin AcaSessionsClient to the user-assigned identity. Matches
              // Microsoft's dynamic-sessions samples (which use a single
              // dedicated user-assigned MI for ACA Sessions) and avoids
              // ambiguity when DefaultAzureCredential / ManagedIdentityCredential
              // picks between the system + user-assigned MIs both attached to
              // the API container. Both identities are granted Session Executor
              // + Contributor on the pool in modules/sessions.bicep, so the
              // unpinned path still works as a fallback.
              name: 'AI__CODEACT__ACASESSIONS__MANAGEDIDENTITYCLIENTID'
              value: apiPullIdentity.properties.clientId
            }
            {
              name: 'AI__CODEACT__NONCESIGNINGKEY'
              secretRef: 'code-act-nonce-signing-key'
            }
          ], apiAdditionalEnvVars)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: environmentName == 'prod' ? 2 : environmentName == 'dev' ? 0 : 1
        maxReplicas: environmentName == 'prod' ? 10 : 3
      }
    }
  }
}

// CodeAct / ACA Dynamic Sessions session pool. Declared after `apiApp` so the
// role assignment can reference `apiApp.identity.principalId`. The pool's
// management endpoint is constructed deterministically above (see
// `codeActPoolManagementEndpoint`) so apiApp's env block doesn't need to
// reference `sessions.outputs.*` and we avoid a circular dependency.
module sessions '../../modules/sessions.bicep' = {
  name: 'sessions-${environmentName}'
  params: {
    workloadName: workloadName
    environmentName: environmentName
    location: location
    tags: tags
    apiPrincipalId: apiApp.identity.principalId
    apiUserAssignedPrincipalId: apiPullIdentity.properties.principalId
  }
}

resource workerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-worker'
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
        {
          name: 'qdrant-api-key'
          value: qdrantApiKey
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
            {
              name: 'Qdrant__Endpoint'
              value: 'http://${qdrantApp.name}'
            }
            {
              name: 'Qdrant__ApiKey'
              secretRef: 'qdrant-api-key'
            }
            {
              name: 'Qdrant__CollectionPrefix'
              value: 'aonik-${environmentName}'
            }
          ], workerAdditionalEnvVars)
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
      scale: {
        minReplicas: environmentName == 'prod' ? 2 : environmentName == 'dev' ? 0 : 1
        maxReplicas: environmentName == 'prod' ? 5 : 2
      }
    }
  }
}

resource adminUiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: adminUiAppName
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
          env: [
            {
              name: 'API_BACKEND_URL'
              value: 'https://${apiAppName}.${containerAppsEnvironment.properties.defaultDomain}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: environmentName == 'prod' ? 2 : environmentName == 'dev' ? 0 : 1
        maxReplicas: environmentName == 'prod' ? 5 : 2
      }
    }
  }
}

// ── Qdrant Vector Store Container App ──────────────────────────────

resource qdrantApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-qdrant'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${qdrantPullIdentity.id}': {}
    }
  }
  dependsOn: [
    acrPullRoleForQdrantPullIdentity
  ]
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        targetPort: 6333
        transport: 'http'
        exposedPort: 0
      }
      secrets: [
        {
          name: 'qdrant-api-key'
          value: qdrantApiKey
        }
        {
          name: 'storage-account-name'
          keyVaultUrl: data.outputs.qdrantStorageAccountNameSecretUri
          identity: 'system'
        }
        {
          name: 'storage-account-key'
          keyVaultUrl: data.outputs.qdrantStorageAccountKeySecretUri
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'qdrant'
          image: qdrantImage
          env: [
            {
              name: 'QDRANT_API_KEY'
              secretRef: 'qdrant-api-key'
            }
            {
              name: 'QDRANT_SNAPSHOT_DIR'
              value: '/qdrant/snapshots'
            }
            {
              name: 'QDRANT_READ_ONLY_API'
              value: 'false'
            }
          ]
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          volumeMounts: [
            {
              volumeName: 'qdrant-storage'
              mountPath: '/qdrant/storage'
            }
            {
              volumeName: 'qdrant-snapshots'
              mountPath: '/qdrant/snapshots'
            }
          ]
          // Qdrant's HTTP API exposes /livez and /readyz (v1.9+). The previous
          // '/health' path returned 404, causing a crash loop that kept Qdrant
          // NotRunning — every user-memory recall then hit a 10s client-side
          // timeout before the agent could start answering.
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/livez'
                port: 6333
              }
              initialDelaySeconds: 30
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/readyz'
                port: 6333
              }
              initialDelaySeconds: 10
              periodSeconds: 5
              timeoutSeconds: 3
              failureThreshold: 2
            }
          ]
        }
      ]
      volumes: [
        {
          name: 'qdrant-storage'
          storageType: 'AzureFile'
          storageName: 'qdrant-storage'
        }
        {
          name: 'qdrant-snapshots'
          storageType: 'AzureFile'
          storageName: 'qdrant-snapshots'
        }
      ]
      scale: {
        minReplicas: environmentName == 'dev' ? 0 : 1
        maxReplicas: 2
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

resource acrPullRoleForQdrantPullIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistryName, qdrantPullIdentity.name, 'AcrPull')
  scope: containerRegistry
  dependsOn: [
    common
  ]
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: qdrantPullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource kvSecretsUserRoleForQdrant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultName, qdrantApp.name, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: qdrantApp.identity.principalId
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

module monitoring '../../modules/monitoring.bicep' = if (alertsEnabled) {
  name: 'monitoring-${environmentName}'
  params: {
    location: location
    workloadName: workloadName
    environmentName: environmentName
    tags: tags
    alertsEnabled: alertsEnabled
    alertsWebhookServiceUri: alertsWebhookServiceUri
    logAnalyticsWorkspaceId: common.outputs.logAnalyticsWorkspaceId
    apiAppResourceId: apiApp.id
    apiAppName: apiApp.name
    workerAppResourceId: workerApp.id
    workerAppName: workerApp.name
    sqlDatabaseResourceId: data.outputs.sqlDatabaseId
    keyVaultResourceId: data.outputs.keyVaultId
  }
}

output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output codeActSessionPoolEndpoint string = sessions.outputs.poolManagementEndpoint
output codeActSessionPoolId string = sessions.outputs.sessionPoolId
output adminUiUrl string = 'https://${adminUiApp.properties.configuration.ingress.fqdn}'
output containerRegistryLoginServer string = common.outputs.containerRegistryLoginServer
output keyVaultName string = data.outputs.keyVaultName
output sqlServerName string = data.outputs.sqlServerName
