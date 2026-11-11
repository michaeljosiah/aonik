@description('Qualified verifier image; always provide an immutable digest from the target registry.')
param verifierImage string

param location string = 'uksouth'
param workloadName string = 'aonik'
@allowed(['dev', 'staging', 'prod'])
param environmentName string = 'dev'
param poolName string = '${workloadName}${environmentName}publishing'
param tags object = {}
@allowed(['standard', 'large-v1'])
param capacityProfile string = 'standard'
var large = capacityProfile == 'large-v1'
var namePrefix = '${workloadName}-${environmentName}'
var resourceTags = union({ environment: environmentName, purpose: 'publishing-verification' }, tags)

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: replace('${namePrefix}acr', '-', '')
}

resource api 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: '${namePrefix}-api'
}
resource worker 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: '${namePrefix}-worker'
}

// No private storage bindings, VNet credentials or existing AI sandbox policy.
resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-publishing-acae'
  location: location
  tags: resourceTags
  properties: {
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
  }
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-publishing-pull-id'
  location: location
}
var acrPullRole = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
resource pull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, pullIdentity.id, acrPullRole)
  properties: {
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRole)
  }
}

resource pool 'Microsoft.App/sessionPools@2025-02-02-preview' = {
  name: poolName
  location: location
  tags: resourceTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${pullIdentity.id}': {} }
  }
  properties: {
    environmentId: environment.id
    containerType: 'CustomContainer'
    poolManagementType: 'Dynamic'
    managedIdentitySettings: [{ identity: pullIdentity.id, lifecycle: 'None' }]
    sessionNetworkConfiguration: { status: 'EgressDisabled' }
    scaleConfiguration: { maxConcurrentSessions: 2, readySessionInstances: 1 }
    dynamicPoolConfiguration: {
      lifecycleConfiguration: { lifecycleType: 'OnContainerExit', maxAlivePeriodInSeconds: large ? 1200 : 300 }
    }
    customContainerTemplate: {
      ingress: { targetPort: 8080 }
      registryCredentials: { server: registry.properties.loginServer, identity: pullIdentity.id }
      containers: [{
        name: 'verifier'
        image: verifierImage
        resources: { cpu: large ? 2 : 1, memory: large ? '4Gi' : '2Gi' }
      }]
    }
  }
  dependsOn: [pull]
}

// Data-plane execution + read-only profile validation, never Contributor.
var roles = ['0fb8eba5-a2bb-4abe-b1c1-49dfad359bb0', 'acdd72a7-3385-48ef-bd42-f606fba81ae7']
resource apiAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in roles: {
  scope: pool
  name: guid(pool.id, api.id, role)
  properties: {
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
  }
}]
resource workerAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in roles: {
  scope: pool
  name: guid(pool.id, worker.id, role)
  properties: {
    principalId: worker.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
  }
}]

output poolResourceId string = pool.id
output poolEndpoint string = pool.properties.poolManagementEndpoint
