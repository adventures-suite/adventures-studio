targetScope = 'resourceGroup'

@minLength(40)
@maxLength(40)
param releaseSha string
param imageDigest string
param workforceTenantId string
param sqlServerFqdn string
param sqlDatabaseName string = 'AdventuresSuiteDevelopment'
param registryResourceId string
param environmentResourceId string
param migrationIdentityResourceId string
param pullIdentityResourceId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = { name: 'advsuitemigrationsdev' }
resource environment 'Microsoft.App/managedEnvironments@2025-01-01' existing = { name: 'cae-adventures-suite-migrations-dev' }
resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-job-dev' }
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-pull-dev' }

var validatedRegistryResourceId = toLower(registryResourceId) == toLower(registry.id) ? registryResourceId : fail('The registry resource ID is not approved.')
var validatedEnvironmentResourceId = toLower(environmentResourceId) == toLower(environment.id) ? environmentResourceId : fail('The Container Apps environment resource ID is not approved.')
var validatedMigrationIdentityResourceId = toLower(migrationIdentityResourceId) == toLower(migrationIdentity.id) ? migrationIdentityResourceId : fail('The migration identity resource ID is not approved.')
var validatedPullIdentityResourceId = toLower(pullIdentityResourceId) == toLower(pullIdentity.id) ? pullIdentityResourceId : fail('The pull identity resource ID is not approved.')
var validatedWorkforceTenantId = toLower(workforceTenantId) == 'd7add2bb-ac03-49a8-9377-d0bf6a012f2f' ? workforceTenantId : fail('The workforce tenant is not approved.')
var validatedSqlServerFqdn = toLower(sqlServerFqdn) == 'adventures-suite-dev-sql.${az.environment().suffixes.sqlServerHostname}' ? sqlServerFqdn : fail('The SQL server is not approved.')
var validatedSqlDatabaseName = sqlDatabaseName == 'AdventuresSuiteDevelopment' ? sqlDatabaseName : fail('The SQL database is not approved.')
var validatedImageDigest = startsWith(imageDigest, 'sha256:') && length(imageDigest) == 71 ? imageDigest : fail('The registry digest is malformed.')

resource job 'Microsoft.App/jobs@2025-01-01' = {
  name: 'job-adventures-suite-migrate-dev'
  location: resourceGroup().location
  tags: { environment: 'development', component: 'database-migrations', managedBy: 'bicep', releaseSha: releaseSha, imageDigest: validatedImageDigest }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${validatedMigrationIdentityResourceId}': {}, '${validatedPullIdentityResourceId}': {} }
  }
  properties: {
    environmentId: validatedEnvironmentResourceId
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 900
      replicaRetryLimit: 0
      manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
      registries: [{ server: registry.properties.loginServer, identity: validatedPullIdentityResourceId }]
    }
    template: {
      containers: [{
        name: 'database-migrator'
        image: '${registry.properties.loginServer}/adventures-suite-database-migrator@${validatedImageDigest}'
        command: ['/app/container-entrypoint.sh']
        args: ['--verify-execution-channel']
        env: [
          { name: 'ADVENTURESSUITE_RELEASE_SHA', value: releaseSha }
          { name: 'ADVENTURESSUITE_IMAGE_DIGEST', value: validatedImageDigest }
          { name: 'ADVENTURESSUITE_MIGRATION_TENANT_ID', value: validatedWorkforceTenantId }
          { name: 'ADVENTURESSUITE_MIGRATION_PRINCIPAL_ID', value: migrationIdentity.properties.principalId }
          { name: 'ADVENTURESSUITE_MIGRATION_PRINCIPAL_CLIENT_ID', value: migrationIdentity.properties.clientId }
          { name: 'ADVENTURESSUITE_MIGRATION_PRINCIPAL_NAME', value: migrationIdentity.name }
          { name: 'ADVENTURESSUITE_SQL_SERVER', value: validatedSqlServerFqdn }
          { name: 'ADVENTURESSUITE_SQL_DATABASE', value: validatedSqlDatabaseName }
        ]
        resources: { cpu: json('0.5'), memory: '1Gi' }
      }]
    }
  }
}
output jobResourceId string = job.id
output deployedImage string = job.properties.template.containers[0].image
output validatedRegistryResourceId string = validatedRegistryResourceId
