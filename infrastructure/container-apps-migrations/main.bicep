targetScope = 'resourceGroup'

@description('Immutable release SHA used for resource tags and evidence.')
@minLength(40)
@maxLength(40)
param releaseSha string

@description('Immutable ACR image digest, including sha256: prefix.')
param imageDigest string

@description('Existing development VNet name.')
param virtualNetworkName string = 'vnet-adventures-suite-dev'

@description('Dedicated delegated Container Apps infrastructure subnet.')
param containerAppsSubnetPrefix string = '10.40.3.0/27'

@description('Workforce tenant ID expected by the migration workload.')
param workforceTenantId string

@description('Expected Azure SQL logical server FQDN.')
param sqlServerFqdn string

@description('Expected Azure SQL database name.')
param sqlDatabaseName string = 'AdventuresSuiteDevelopment'

var location = resourceGroup().location
var registryName = 'advsuitemigrationsdev'
var environmentName = 'cae-adventures-suite-migrations-dev'
var jobName = 'job-adventures-suite-migrate-dev'
var logName = 'log-adventures-suite-migrations-dev'
var migrationIdentityName = 'id-adventures-suite-migrate-job-dev'
var pullIdentityName = 'id-adventures-suite-migrate-pull-dev'
var commonTags = {
  environment: 'development'
  component: 'database-migrations'
  managedBy: 'bicep'
  releaseSha: releaseSha
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: virtualNetworkName
}

resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: vnet
  name: 'snet-container-apps-migrations'
  properties: {
    addressPrefix: containerAppsSubnetPrefix
    delegations: [
      {
        name: 'Microsoft.App.environments'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logName
  location: location
  tags: commonTags
  properties: {
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
  sku: {
    name: 'PerGB2018'
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  tags: commonTags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: migrationIdentityName
  location: location
  tags: commonTags
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: pullIdentityName
  location: location
  tags: commonTags
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: pullIdentity.properties.principalId
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
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource job 'Microsoft.App/jobs@2025-01-01' = {
  name: jobName
  location: location
  tags: commonTags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${migrationIdentity.id}': {}
      '${pullIdentity.id}': {}
    }
  }
  properties: {
    environmentId: environment.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 900
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: pullIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'database-migrator'
          image: '${registry.properties.loginServer}/adventures-suite-database-migrator@${imageDigest}'
          command: [ '/app/container-entrypoint.sh' ]
          args: [ '--verify-execution-channel' ]
          env: [
            { name: 'ADVENTURESSUITE_RELEASE_SHA', value: releaseSha }
            { name: 'ADVENTURESSUITE_IMAGE_DIGEST', value: imageDigest }
            { name: 'ADVENTURESSUITE_MIGRATION_TENANT_ID', value: workforceTenantId }
            { name: 'ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID', value: migrationIdentity.properties.principalId }
            { name: 'ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID', value: migrationIdentity.properties.clientId }
            { name: 'ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME', value: migrationIdentity.name }
            { name: 'ADVENTURESSUITE_SQL_SERVER', value: sqlServerFqdn }
            { name: 'ADVENTURESSUITE_SQL_DATABASE', value: sqlDatabaseName }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

output migrationIdentityPrincipalId string = migrationIdentity.properties.principalId
output migrationIdentityClientId string = migrationIdentity.properties.clientId
output pullIdentityPrincipalId string = pullIdentity.properties.principalId
output registryLoginServer string = registry.properties.loginServer
output jobResourceId string = job.id
output subnetPrefix string = containerAppsSubnetPrefix
