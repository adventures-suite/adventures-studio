targetScope = 'resourceGroup'

@description('Existing development VNet name.')
param virtualNetworkName string = 'vnet-adventures-suite-dev'
@description('Dedicated delegated Container Apps infrastructure subnet.')
param containerAppsSubnetPrefix string = '10.40.3.0/27'
param migrationIdentityResourceId string
param pullIdentityResourceId string
param publisherIdentityResourceId string
param starterIdentityResourceId string

var validatedVirtualNetworkName = virtualNetworkName == 'vnet-adventures-suite-dev' ? virtualNetworkName : fail('The virtual network is not approved.')
var validatedSubnetPrefix = containerAppsSubnetPrefix == '10.40.3.0/27' ? containerAppsSubnetPrefix : fail('The migration subnet prefix is not approved.')
var location = resourceGroup().location
var commonTags = { environment: 'development', component: 'database-migrations', managedBy: 'bicep' }

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = { name: validatedVirtualNetworkName }
resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: vnet
  name: 'snet-container-apps-migrations'
  properties: {
    addressPrefix: validatedSubnetPrefix
    delegations: [{ name: 'Microsoft.App.environments', properties: { serviceName: 'Microsoft.App/environments' } }]
    privateEndpointNetworkPolicies: 'Disabled'
  }
}
resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-adventures-suite-migrations-dev'
  location: location
  tags: commonTags
  properties: { retentionInDays: 30, features: { enableLogAccessUsingOnlyResourcePermissions: true } }
  sku: { name: 'PerGB2018' }
}
resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'advsuitemigrationsdev'
  location: location
  tags: commonTags
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false, publicNetworkAccess: 'Enabled' }
}
resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-job-dev' }
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-pull-dev' }
resource publisherIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-publisher-dev' }
resource starterIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-starter-dev' }

var validatedMigrationIdentityResourceId = toLower(migrationIdentityResourceId) == toLower(migrationIdentity.id) ? migrationIdentityResourceId : fail('The migration identity resource ID is not approved.')
var validatedPullIdentityResourceId = toLower(pullIdentityResourceId) == toLower(pullIdentity.id) ? pullIdentityResourceId : fail('The pull identity resource ID is not approved.')
var validatedPublisherIdentityResourceId = toLower(publisherIdentityResourceId) == toLower(publisherIdentity.id) ? publisherIdentityResourceId : fail('The publisher identity resource ID is not approved.')
var validatedStarterIdentityResourceId = toLower(starterIdentityResourceId) == toLower(starterIdentity.id) ? starterIdentityResourceId : fail('The starter identity resource ID is not approved.')
resource environment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: 'cae-adventures-suite-migrations-dev'
  location: location
  tags: commonTags
  properties: {
    vnetConfiguration: { infrastructureSubnetId: subnet.id, internal: false }
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: { customerId: logs.properties.customerId, sharedKey: logs.listKeys().primarySharedKey }
    }
  }
}

output registryResourceId string = registry.id
output registryLoginServer string = registry.properties.loginServer
output logWorkspaceResourceId string = logs.id
output environmentResourceId string = environment.id
output migrationIdentityResourceId string = validatedMigrationIdentityResourceId
output migrationIdentityPrincipalId string = migrationIdentity.properties.principalId
output migrationIdentityClientId string = migrationIdentity.properties.clientId
output pullIdentityResourceId string = validatedPullIdentityResourceId
output publisherIdentityResourceId string = validatedPublisherIdentityResourceId
output starterIdentityResourceId string = validatedStarterIdentityResourceId
