targetScope = 'resourceGroup'

@description('Exact reviewed repository permitted to federate with GitHub Actions.')
param githubRepository string = 'ssimonton007/adventures-studio'

@description('Existing development VNet name.')
param virtualNetworkName string = 'vnet-adventures-suite-dev'

@description('Dedicated delegated Container Apps infrastructure subnet.')
param containerAppsSubnetPrefix string = '10.40.3.0/27'

var location = resourceGroup().location
var registryName = 'advsuitemigrationsdev'
var environmentName = 'cae-adventures-suite-migrations-dev'
var environmentSubject = 'repo:${githubRepository}:environment:database-development'
var commonTags = {
  environment: 'development'
  component: 'database-migrations'
  managedBy: 'bicep'
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: virtualNetworkName
}

resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: vnet
  name: 'snet-container-apps-migrations'
  properties: {
    addressPrefix: containerAppsSubnetPrefix
    delegations: [{
      name: 'Microsoft.App.environments'
      properties: { serviceName: 'Microsoft.App/environments' }
    }]
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-adventures-suite-migrations-dev'
  location: location
  tags: commonTags
  properties: {
    retentionInDays: 30
    features: { enableLogAccessUsingOnlyResourcePermissions: true }
  }
  sku: { name: 'PerGB2018' }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  tags: commonTags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
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

resource configuratorIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-adventures-suite-migrate-configurator-dev'
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
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: environmentSubject
    audiences: ['api://AzureADTokenExchange']
  }
}

resource configuratorFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: configuratorIdentity
  name: 'github-database-development'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: environmentSubject
    audiences: ['api://AzureADTokenExchange']
  }
}

resource starterFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: starterIdentity
  name: 'github-database-development'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: environmentSubject
    audiences: ['api://AzureADTokenExchange']
  }
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, publisherIdentity.id, 'AcrPush')
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8311e382-0749-4cb8-b61a-304f252e45ec')
    principalId: publisherIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource configuratorRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'migration-job-configurator')
  properties: {
    roleName: 'AdventuresSuite Migration Job Configurator'
    description: 'Creates or updates only reviewed Container Apps Job definitions.'
    type: 'CustomRole'
    assignableScopes: [resourceGroup().id]
    permissions: [{
      actions: [
        'Microsoft.Resources/deployments/read'
        'Microsoft.Resources/deployments/write'
        'Microsoft.Resources/deployments/validate/action'
        'Microsoft.Resources/deployments/operationStatuses/read'
        'Microsoft.Resources/deployments/operations/read'
        'Microsoft.App/jobs/read'
        'Microsoft.App/jobs/write'
        'Microsoft.App/managedEnvironments/read'
        'Microsoft.ContainerRegistry/registries/read'
        'Microsoft.ManagedIdentity/userAssignedIdentities/read'
        'Microsoft.ManagedIdentity/userAssignedIdentities/assign/action'
      ]
      notActions: ['Microsoft.App/jobs/start/action']
      dataActions: []
      notDataActions: []
    }]
  }
}

resource starterRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'migration-job-starter-reader')
  properties: {
    roleName: 'AdventuresSuite Migration Job Starter Reader'
    description: 'Starts and observes the exact reviewed migration Job execution.'
    type: 'CustomRole'
    assignableScopes: [resourceGroup().id]
    permissions: [{
      actions: [
        'Microsoft.App/jobs/read'
        'Microsoft.App/jobs/start/action'
        'Microsoft.App/jobs/executions/read'
        'Microsoft.App/jobs/executions/replicas/read'
        'Microsoft.OperationalInsights/workspaces/query/read'
      ]
      notActions: ['Microsoft.App/jobs/write', 'Microsoft.App/jobs/delete']
      dataActions: []
      notDataActions: []
    }]
  }
}

resource configuratorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, configuratorIdentity.id, configuratorRole.id)
  properties: {
    roleDefinitionId: configuratorRole.id
    principalId: configuratorIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource starterAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, starterIdentity.id, starterRole.id)
  properties: {
    roleDefinitionId: starterRole.id
    principalId: starterIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource environment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: environmentName
  location: location
  tags: commonTags
  properties: {
    vnetConfiguration: {
      infrastructureSubnetId: subnet.id
      internal: false
    }
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

output registryLoginServer string = registry.properties.loginServer
output migrationIdentityPrincipalId string = migrationIdentity.properties.principalId
output migrationIdentityClientId string = migrationIdentity.properties.clientId
output publisherClientId string = publisherIdentity.properties.clientId
output configuratorClientId string = configuratorIdentity.properties.clientId
output starterClientId string = starterIdentity.properties.clientId
output environmentResourceId string = environment.id
