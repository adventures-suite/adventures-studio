targetScope = 'subscription'

var subscriptionScope = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a'
var registrarCatalog = loadJsonContent('./roles/provider-registration.role.json')

resource registrarRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: registrarCatalog.name
  properties: registrarCatalog.properties.assignableScopes[0] == subscriptionScope
    ? registrarCatalog.properties
    : fail('Provider registrar scope is not approved.')
}

output providerRegistrarRoleDefinitionId string = registrarRole.id
