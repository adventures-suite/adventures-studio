targetScope = 'resourceGroup'

var infrastructureRole = loadJsonContent('./roles/infrastructure-deployer.role.json')
var identityReaderRole = loadJsonContent('./roles/identity-reader.role.json')
var approvedScope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev'

resource infrastructureRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: infrastructureRole.name
  properties: {
    roleName: infrastructureRole.properties.roleName
    description: infrastructureRole.properties.description
    type: infrastructureRole.properties.type
    permissions: infrastructureRole.properties.permissions
    assignableScopes: infrastructureRole.properties.assignableScopes[0] == approvedScope
      ? infrastructureRole.properties.assignableScopes
      : fail('Infrastructure deployer assignable scope is not approved.')
  }
}

resource identityReaderRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: identityReaderRole.name
  properties: {
    roleName: identityReaderRole.properties.roleName
    description: identityReaderRole.properties.description
    type: identityReaderRole.properties.type
    permissions: identityReaderRole.properties.permissions
    assignableScopes: identityReaderRole.properties.assignableScopes[0] == approvedScope
      ? identityReaderRole.properties.assignableScopes
      : fail('Identity reader assignable scope is not approved.')
  }
}

output infrastructureRoleDefinitionId string = infrastructureRoleDefinition.id
output identityReaderRoleDefinitionId string = identityReaderRoleDefinition.id
