targetScope = 'resourceGroup'

@minLength(40)
@maxLength(40)
param releaseSha string

@description('Registry-authoritative immutable image digest including sha256: prefix.')
param imageDigest string
param workforceTenantId string
param sqlServerFqdn string
param sqlDatabaseName string = 'AdventuresSuiteDevelopment'

var registryName = 'advsuitemigrationsdev'
var environmentName = 'cae-adventures-suite-migrations-dev'
var migrationIdentityName = 'id-adventures-suite-migrate-job-dev'
var pullIdentityName = 'id-adventures-suite-migrate-pull-dev'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = { name: registryName }
resource environment 'Microsoft.App/managedEnvironments@2025-01-01' existing = { name: environmentName }
resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: migrationIdentityName }
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: pullIdentityName }

resource job 'Microsoft.App/jobs@2025-01-01' = {
  name: 'job-adventures-suite-migrate-dev'
  location: resourceGroup().location
  tags: {
    environment: 'development'
    component: 'database-migrations'
    managedBy: 'bicep'
    releaseSha: releaseSha
    imageDigest: imageDigest
  }
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
      manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
      registries: [{ server: registry.properties.loginServer, identity: pullIdentity.id }]
    }
    template: {
      containers: [{
        name: 'database-migrator'
        image: '${registry.properties.loginServer}/adventures-suite-database-migrator@${imageDigest}'
        command: ['/app/container-entrypoint.sh']
        args: ['--verify-execution-channel']
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
        resources: { cpu: json('0.5'), memory: '1Gi' }
      }]
    }
  }
}

// Operation ID and artifact checksum are deliberately absent from this persistent template.
// The starter supplies both as one-execution overrides to `az containerapp job start`.
output jobResourceId string = job.id
output deployedImage string = job.properties.template.containers[0].image
