targetScope = 'resourceGroup'

param registryResourceId string
param logWorkspaceResourceId string
param pullIdentityResourceId string
param publisherIdentityResourceId string
param starterIdentityResourceId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = { name: 'advsuitemigrationsdev' }
resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = { name: 'log-adventures-suite-migrations-dev' }
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-pull-dev' }
resource publisherIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-publisher-dev' }
resource starterIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-starter-dev' }

var validatedRegistryResourceId = toLower(registryResourceId) == toLower(registry.id) ? registryResourceId : fail('The registry resource ID is not approved.')
var validatedLogWorkspaceResourceId = toLower(logWorkspaceResourceId) == toLower(logs.id) ? logWorkspaceResourceId : fail('The Log Analytics workspace resource ID is not approved.')
var validatedPullIdentityResourceId = toLower(pullIdentityResourceId) == toLower(pullIdentity.id) ? pullIdentityResourceId : fail('The pull identity resource ID is not approved.')
var validatedPublisherIdentityResourceId = toLower(publisherIdentityResourceId) == toLower(publisherIdentity.id) ? publisherIdentityResourceId : fail('The publisher identity resource ID is not approved.')
var validatedStarterIdentityResourceId = toLower(starterIdentityResourceId) == toLower(starterIdentity.id) ? starterIdentityResourceId : fail('The starter identity resource ID is not approved.')

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(validatedRegistryResourceId, validatedPullIdentityResourceId, 'AcrPull')
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource acrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(validatedRegistryResourceId, validatedPublisherIdentityResourceId, 'AcrPush')
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8311e382-0749-4cb8-b61a-304f252e45ec')
    principalId: publisherIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
resource starterRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'migration-job-starter-reader')
  properties: {
    roleName: 'AdventuresSuite Migration Job Starter Reader'
    description: 'Starts and observes only an exact-scoped reviewed migration Job.'
    type: 'CustomRole'
    assignableScopes: [resourceGroup().id]
    permissions: [{
      actions: ['Microsoft.App/jobs/read', 'Microsoft.App/jobs/start/action', 'Microsoft.App/jobs/execution/read', 'Microsoft.App/jobs/executions/read']
      notActions: []
      dataActions: []
      notDataActions: []
    }]
  }
}
resource logReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(validatedLogWorkspaceResourceId, validatedStarterIdentityResourceId, 'Log Analytics Reader')
  scope: logs
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '73c42c96-874c-492b-b04d-ab87d138a893')
    principalId: starterIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output starterRoleDefinitionId string = starterRole.id
output validatedPublisherIdentityResourceId string = validatedPublisherIdentityResourceId
