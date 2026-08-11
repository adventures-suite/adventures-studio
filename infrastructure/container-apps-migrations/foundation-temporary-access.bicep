targetScope = 'resourceGroup'

@description('Exact foundation deployer principal/object ID.')
param foundationDeployerPrincipalId string

var approvedFoundationPrincipalId = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8'
var infrastructureRoleUuid = '4bfa5b8d-8e4a-4fc8-9f2b-6115f07cad54'
var identityReaderRoleUuid = '9df6bf68-4db7-4d38-b7f1-7bb26a541199'
var validatedPrincipalId = toLower(foundationDeployerPrincipalId) == approvedFoundationPrincipalId
  ? foundationDeployerPrincipalId
  : fail('Foundation deployer principal is not approved.')

resource infrastructureRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: infrastructureRoleUuid
}

resource identityReaderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: identityReaderRoleUuid
}

resource infrastructureAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: '5c14d19b-04c7-4dfa-83ed-9447d0ea3c33'
  properties: {
    principalId: validatedPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: infrastructureRole.id
  }
}

resource identityReaderAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: 'fa329695-3907-4852-94f5-fda8a26a4698'
  properties: {
    principalId: validatedPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: identityReaderRole.id
  }
}

output infrastructureAssignmentId string = infrastructureAssignment.id
output identityReaderAssignmentId string = identityReaderAssignment.id
