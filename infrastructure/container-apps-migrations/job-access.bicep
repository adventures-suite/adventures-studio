targetScope = 'resourceGroup'

param jobResourceId string
param starterIdentityResourceId string
param starterRoleDefinitionId string

resource job 'Microsoft.App/jobs@2025-01-01' existing = { name: 'job-adventures-suite-migrate-dev' }
resource starterIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-starter-dev' }
resource starterRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = { name: guid(resourceGroup().id, 'migration-job-starter-reader') }

var validatedJobResourceId = toLower(jobResourceId) == toLower(job.id) ? jobResourceId : fail('The Job resource ID is not approved.')
var validatedStarterIdentityResourceId = toLower(starterIdentityResourceId) == toLower(starterIdentity.id) ? starterIdentityResourceId : fail('The starter identity resource ID is not approved.')
var validatedStarterRoleDefinitionId = toLower(starterRoleDefinitionId) == toLower(starterRole.id) ? starterRoleDefinitionId : fail('The starter role definition ID is not approved.')

resource starterAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(validatedJobResourceId, validatedStarterIdentityResourceId, validatedStarterRoleDefinitionId)
  scope: job
  properties: {
    roleDefinitionId: validatedStarterRoleDefinitionId
    principalId: starterIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}
