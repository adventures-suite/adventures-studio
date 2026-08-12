targetScope = 'subscription'

@description('Exact foundation-deployer principal/object ID.')
param foundationDeployerPrincipalId string

var approvedPrincipalId = 'b77b6201-ad26-4f77-8f88-6d0d43f7dbb8'
var registrarRoleUuid = 'fcdbbdc4-b56a-4863-aebb-32790e5b1a51'
var validatedPrincipalId = toLower(foundationDeployerPrincipalId) == approvedPrincipalId
  ? foundationDeployerPrincipalId
  : fail('Provider registration principal is not approved.')

resource registrarRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = { name: registrarRoleUuid }
resource registrarAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: '3327e40f-74ee-42e5-a0ee-e8002b125cb3'
  properties: {
    principalId: validatedPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: registrarRole.id
  }
}

output providerRegistrationAssignmentId string = registrarAssignment.id
