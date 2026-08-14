targetScope = 'resourceGroup'

@description('Exact post-creation residue-reader principal ID; no default.')
param residueReaderPrincipalId string

var roleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'eff3d13d-aeac-4b96-94f8-9c03a1ceee69')
resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, residueReaderPrincipalId, roleId)
  properties: { principalId: residueReaderPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId }
}
output assignmentId string = assignment.id
