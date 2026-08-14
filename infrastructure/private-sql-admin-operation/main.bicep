targetScope = 'resourceGroup'

@description('Unique reviewed one-operation identifier.')
param operationId string
@description('Immutable resource ID of the existing reviewed VNet.')
param existingVnetResourceId string
@description('Immutable resource ID of the dedicated SQL administrator UAMI.')
param administratorIdentityResourceId string
@description('Immutable resource ID of the distinct migration UAMI, used only for inequality validation.')
param migrationIdentityResourceId string
param adminUsername string
@secure()
param ephemeralAdminSshPublicKey string
@secure()
param bootstrapCustomData string

var identitiesAreDistinct = toLower(administratorIdentityResourceId) != toLower(migrationIdentityResourceId)

module reviewedRunner '../private-migration-runner/main.bicep' = if (identitiesAreDistinct) {
  name: 'private-sql-admin-runner-${operationId}'
  params: {
    operationId: operationId
    existingVnetResourceId: existingVnetResourceId
    migrationIdentityResourceId: administratorIdentityResourceId
    adminUsername: adminUsername
    ephemeralAdminSshPublicKey: ephemeralAdminSshPublicKey
    bootstrapCustomData: bootstrapCustomData
  }
}

output operationId string = operationId
output identitiesAreDistinct bool = identitiesAreDistinct
output resolvedAdministratorIdentityResourceId string = reviewedRunner!.outputs.resolvedMigrationIdentityResourceId
output resourceIds array = reviewedRunner!.outputs.resourceIds
