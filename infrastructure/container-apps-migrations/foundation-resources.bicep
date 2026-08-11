targetScope = 'resourceGroup'

@description('Exact reviewed repository permitted to federate with GitHub Actions.')
param githubRepository string = 'ssimonton007/adventures-studio'
@description('Existing development VNet name.')
param virtualNetworkName string = 'vnet-adventures-suite-dev'
@description('Dedicated delegated Container Apps infrastructure subnet.')
param containerAppsSubnetPrefix string = '10.40.3.0/27'

var validatedGithubRepository = githubRepository == 'ssimonton007/adventures-studio' ? githubRepository : fail('The GitHub repository is not approved.')
var validatedVirtualNetworkName = virtualNetworkName == 'vnet-adventures-suite-dev' ? virtualNetworkName : fail('The virtual network is not approved.')
var validatedSubnetPrefix = containerAppsSubnetPrefix == '10.40.3.0/27' ? containerAppsSubnetPrefix : fail('The migration subnet prefix is not approved.')
var location = resourceGroup().location
var environmentSubject = 'repo:${validatedGithubRepository}:environment:database-development'
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
resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-adventures-suite-migrate-job-dev'
  location: location
  tags: commonTags
}
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-adventures-suite-migrate-pull-dev'
  location: location
  tags: commonTags
}
resource publisherIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-adventures-suite-migrate-publisher-dev'
  location: location
  tags: commonTags
}
resource starterIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-adventures-suite-migrate-starter-dev'
  location: location
  tags: commonTags
}
resource publisherFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: publisherIdentity
  name: 'github-database-development'
  properties: { issuer: 'https://token.actions.githubusercontent.com', subject: environmentSubject, audiences: ['api://AzureADTokenExchange'] }
}
resource starterFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: starterIdentity
  name: 'github-database-development'
  properties: { issuer: 'https://token.actions.githubusercontent.com', subject: environmentSubject, audiences: ['api://AzureADTokenExchange'] }
}
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
output migrationIdentityResourceId string = migrationIdentity.id
output migrationIdentityPrincipalId string = migrationIdentity.properties.principalId
output migrationIdentityClientId string = migrationIdentity.properties.clientId
output pullIdentityResourceId string = pullIdentity.id
output publisherIdentityResourceId string = publisherIdentity.id
output starterIdentityResourceId string = starterIdentity.id
