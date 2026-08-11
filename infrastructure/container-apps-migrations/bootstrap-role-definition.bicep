targetScope = 'resourceGroup'

var bootstrapRole = loadJsonContent('./roles/rbac-role-definition-deployer.role.json')
var approvedScope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev'

resource bootstrapRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: bootstrapRole.name
  properties: {
    roleName: bootstrapRole.properties.roleName
    description: bootstrapRole.properties.description
    type: bootstrapRole.properties.type
    permissions: bootstrapRole.properties.permissions
    assignableScopes: bootstrapRole.properties.assignableScopes[0] == approvedScope
      ? bootstrapRole.properties.assignableScopes
      : fail('Bootstrap role assignable scope is not approved.')
  }
}

output bootstrapRoleDefinitionId string = bootstrapRoleDefinition.id
