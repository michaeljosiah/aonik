targetScope = 'resourceGroup'

// Standalone dev prerequisite for invitations and verification emails. No DNS
// changes, production sender, credentials, or application settings are deployed.
var tags = {
  environment: 'dev'
  workload: 'aonik'
  purpose: 'registration-rehearsal'
}

resource email 'Microsoft.Communication/emailServices@2023-03-31' = {
  name: 'aonik-dev-email'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: 'UK'
  }
}

resource domain 'Microsoft.Communication/emailServices/domains@2023-03-31' = {
  parent: email
  name: 'AzureManagedDomain'
  location: 'global'
  tags: tags
  properties: {
    domainManagement: 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

resource communication 'Microsoft.Communication/communicationServices@2023-03-31' = {
  name: 'aonik-dev-acs'
  location: 'global'
  tags: tags
  properties: {
    dataLocation: 'UK'
    linkedDomains: [domain.id]
  }
}

output communicationResourceId string = communication.id
output domainResourceId string = domain.id
