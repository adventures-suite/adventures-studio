targetScope = 'resourceGroup'

@description('Exact post-creation provisioner principal ID; no default.')
param provisionerPrincipalId string

var roleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '36895920-b36b-4b0c-8a6a-6762164de71e')
resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, provisionerPrincipalId, roleId)
  properties: { principalId: provisionerPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId }
}
output assignmentId string = assignment.id
