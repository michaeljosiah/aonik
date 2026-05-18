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

@description('System principal id (from apiApp.identity.principalId) to grant the Azure ContainerApps Session Executor role to.')
param apiPrincipalId string

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

// Built-in role: Azure ContainerApps Session Executor
// (https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#azure-containerapps-session-executor).
// Required to invoke /executions; the apiApp's system-assigned identity gets
// this so DefaultAzureCredential in AcaSessionsClient picks up a usable token.
var sessionExecutorRoleDefinitionId = '0fb8eba5-a2bb-4abe-b1c1-49dfad359bb0'

resource sessionExecutorRoleForApi 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: sessionPool
  name: guid(sessionPool.id, apiPrincipalId, sessionExecutorRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', sessionExecutorRoleDefinitionId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('Pool management endpoint, e.g. https://<region>.dynamicsessions.io/subscriptions/<sub>/resourceGroups/<rg>/sessionPools/<name>. Pass into AI__CODEACT__ACASESSIONS__POOLMANAGEMENTENDPOINT.')
output poolManagementEndpoint string = sessionPool.properties.poolManagementEndpoint

@description('Resource id, useful for diagnostics / role assignment scoping.')
output sessionPoolId string = sessionPool.id

@description('Echo of the resolved API version so consumers can stamp it on requests if desired.')
output apiVersion string = sessionPoolApiVersion
