// =============================================================================
// Azure Container Apps Dynamic Sessions — system code-interpreter pool
//
// Backs the AcaSessionsCodeActSandboxProvider in Aonik.Finance: Hyper-V-isolated
// Python sandbox the personal-finance sub-agents (pf-insights/forecast/classify)
// use to run a single execute_code call that replaces 50+ sequential tool
// invocations (Spec 025).
//
// The session pool is independent of the container-apps environment that hosts
// our API + worker + adminui — it has its own management endpoint (the
// `poolManagementEndpoint` output) that we hit over REST with a managed-identity
// Bearer token. Microsoft Entra audience: `https://dynamicsessions.io`.
//
// Region note: deploy in `uksouth` if supported, else `northeurope`. Verify with
// `az containerapp sessionpool list-supported-regions` (preview extension)
// before bicep PR. The `location` param defaults to the parent stack's region
// so the deployment fails loudly if the region rejects the resource type.
// =============================================================================

@description('Short workload name used in resource naming.')
param workloadName string

@description('Deployment environment name (dev/staging/prod).')
param environmentName string

@description('Azure region for the session pool.')
param location string

@description('Resource tags applied to the session pool.')
param tags object = {}

@description('System-assigned principal id (apiApp.identity.principalId). Granted Session Executor + Contributor on the pool so DefaultAzureCredential still works if it ever picks the system-assigned identity.')
param apiPrincipalId string

@description('User-assigned principal id (apiPullIdentity.properties.principalId). This is the identity AcaSessionsClient is pinned to via AI__CODEACT__ACASESSIONS__MANAGEDIDENTITYCLIENTID — it must hold the Session Executor + Contributor roles on the pool.')
param apiUserAssignedPrincipalId string = ''

@description('Maximum concurrent sessions the pool can host. Each session is one Hyper-V Python sandbox.')
param maxConcurrentSessions int = 50

@description('Idle cooldown in seconds before a session is stopped. Lower = lower bill; higher = more warm reuse.')
param cooldownSeconds int = 300

@description('ARM API version for Microsoft.App/sessionPools. Pinned here so we can bump without code changes when Microsoft GAs a new version. Verify via the az provider show command before bumping.')
param sessionPoolApiVersion string = '2024-08-02-preview'

var namePrefix = toLower('${workloadName}-${environmentName}')
var sessionPoolName = '${namePrefix}-sessions'

// Microsoft.App/sessionPools — system code interpreter pool.
// Container type `PythonLTS` ships with NumPy/pandas/scikit-learn preinstalled
// (see https://learn.microsoft.com/en-us/azure/container-apps/sessions-code-interpreter).
//
// The pool itself has no managed-identity binding — `PythonLTS` rejects
// `identity`/`managedIdentitySettings` with SessionPoolManagedIdentityCreationError
// (that capability is CustomContainer-only). Caller auth relies entirely on
// the Session Executor + Contributor RBAC role assignments below.
resource sessionPool 'Microsoft.App/sessionPools@2024-08-02-preview' = {
  name: sessionPoolName
  location: location
  tags: tags
  properties: {
    // `Dynamic` is the only valid poolManagementType for the system code
    // interpreter pool (the API rejects `System` with
    // SessionPoolInvalidPoolManagementType).
    poolManagementType: 'Dynamic'
    containerType: 'PythonLTS'
    scaleConfiguration: {
      maxConcurrentSessions: maxConcurrentSessions
    }
    dynamicPoolConfiguration: {
      executionType: 'Timed'
      cooldownPeriodInSeconds: cooldownSeconds
    }
    sessionNetworkConfiguration: {
      // `EgressEnabled` is required so the Python preamble can POST back to
      // our callback endpoint (`/ai/codeact/call-tool/{nonce}`). The nonce is
      // the only auth on that endpoint, so a leaked URL is mitigated by
      // HMAC signing + tool whitelist + budget cap rather than by network
      // restriction. If we tighten this later, switch to a VNet-integrated
      // pool with explicit egress allowlist to our API FQDN only.
      status: 'EgressEnabled'
    }
  }
}

// ACA Dynamic Sessions caller auth — per
// https://learn.microsoft.com/en-us/azure/container-apps/sessions-code-interpreter#authentication
// the calling identity must hold BOTH "Azure ContainerApps Session Executor"
// and "Contributor" on the session pool. Granting only Session Executor
// produces HTTP 401 from the data-plane endpoint.
var sessionExecutorRoleDefinitionId = '0fb8eba5-a2bb-4abe-b1c1-49dfad359bb0'
var contributorRoleDefinitionId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

resource sessionExecutorRoleForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sessionPool
  name: guid(sessionPool.id, apiPrincipalId, sessionExecutorRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sessionExecutorRoleDefinitionId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource contributorRoleForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sessionPool
  name: guid(sessionPool.id, apiPrincipalId, contributorRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleDefinitionId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Grant the same roles to the user-assigned identity (apiPullIdentity).
// AcaSessionsClient is pinned to this identity via
// AI__CODEACT__ACASESSIONS__MANAGEDIDENTITYCLIENTID (matches Microsoft's
// dynamic-sessions samples, which favour a single dedicated user-assigned
// MI for ACA Sessions); the assignments above on the system-assigned
// identity are belt-and-braces for the unpinned/legacy code path.
resource sessionExecutorRoleForApiPullIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(apiUserAssignedPrincipalId)) {
  scope: sessionPool
  name: guid(sessionPool.id, apiUserAssignedPrincipalId, sessionExecutorRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sessionExecutorRoleDefinitionId)
    principalId: apiUserAssignedPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource contributorRoleForApiPullIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(apiUserAssignedPrincipalId)) {
  scope: sessionPool
  name: guid(sessionPool.id, apiUserAssignedPrincipalId, contributorRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleDefinitionId)
    principalId: apiUserAssignedPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('Pool management endpoint, e.g. https://<region>.dynamicsessions.io/subscriptions/<sub>/resourceGroups/<rg>/sessionPools/<name>. Pass into AI__CODEACT__ACASESSIONS__POOLMANAGEMENTENDPOINT.')
output poolManagementEndpoint string = sessionPool.properties.poolManagementEndpoint

@description('Resource id, useful for diagnostics / role assignment scoping.')
output sessionPoolId string = sessionPool.id

@description('Echo of the resolved API version so consumers can stamp it on requests if desired.')
output apiVersion string = sessionPoolApiVersion
