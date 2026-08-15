targetScope = 'subscription'

@description('Exact approved resource-group resource ID; no default.')
param assignableResourceGroupId string

var provisioner = loadJsonContent('provisioner-role-actions.json')
var cleanup = loadJsonContent('cleanup-role-actions.json')
var residueReader = loadJsonContent('residue-reader-role-actions.json')

resource provisionerRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: provisioner.roleUuid
  properties: {
    roleName: provisioner.name
    description: 'Reviewed broker foundation create/reconcile authority. No delete, authorization, identity, credential, or data-plane authority.'
    type: 'CustomRole'
    permissions: [{ actions: provisioner.actions, notActions: [], dataActions: [], notDataActions: [] }]
    assignableScopes: [assignableResourceGroupId]
  }
}

resource cleanupRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: cleanup.roleUuid
  properties: {
    roleName: cleanup.name
    description: 'Delete-only broker foundation authority, assignable only at exact post-deployment resource IDs.'
    type: 'CustomRole'
    permissions: [{ actions: cleanup.actions, notActions: [], dataActions: [], notDataActions: [] }]
    assignableScopes: [assignableResourceGroupId]
  }
}

resource residueReaderRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: residueReader.roleUuid
  properties: {
    roleName: residueReader.name
    description: 'Type-limited read-only broker foundation residue verification.'
    type: 'CustomRole'
    permissions: [{ actions: residueReader.actions, notActions: [], dataActions: [], notDataActions: [] }]
    assignableScopes: [assignableResourceGroupId]
  }
}

output provisionerRoleDefinitionId string = provisionerRole.id
output cleanupRoleDefinitionId string = cleanupRole.id
output residueReaderRoleDefinitionId string = residueReaderRole.id
