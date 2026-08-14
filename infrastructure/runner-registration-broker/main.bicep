targetScope = 'resourceGroup'

@description('Exact future operation deployment identifier; no default.')
@minLength(3)
param deploymentId string
@description('Exact audited Azure Functions runtime stack version; no default.')
param functionRuntimeVersion string
@description('Exact immutable Key Vault secret-version resource ID; no default.')
param githubAppKeySecretVersionResourceId string
@description('Exact future GitHub App ID; no default.')
param githubAppId string
@description('Exact future selected-repository installation ID; no default.')
param githubInstallationId string
@description('Exact reviewed repository runner-group ID; no default.')
param runnerGroupId int
@description('Exact future broker audience; no default.')
param brokerAudience string
@description('Exact future broker hostname; no default.')
param brokerHostname string
@description('Exact existing VNet resource ID for future private storage/Key Vault access; no default.')
param existingVnetResourceId string
@description('Exact existing Key Vault resource ID; no default.')
param existingKeyVaultResourceId string
@description('Exact future broker outbound-integration subnet resource ID; no default.')
param brokerIntegrationSubnetResourceId string
@description('Exact future private-endpoint subnet resource ID; no default.')
param privateEndpointSubnetResourceId string
@description('Exact existing privatelink.table.core.windows.net private DNS zone resource ID; no default.')
param tablePrivateDnsZoneResourceId string

var tags = { purpose: 'ephemeral-runner-registration-broker', persistentCompute: 'false' }
var storageName = 'strunbroker${uniqueString(resourceGroup().id, deploymentId)}'
var appName = 'func-runner-broker-${deploymentId}'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: resourceGroup().location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: { allowBlobPublicAccess: false, allowSharedKeyAccess: false, minimumTlsVersion: 'TLS1_2', publicNetworkAccess: 'Disabled' }
}
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = { parent: storage, name: 'default' }
resource operationTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = { parent: tableService, name: 'RunnerOperations' }
resource tablePrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-runner-broker-table-${deploymentId}'
  location: resourceGroup().location
  tags: tags
  properties: {
    subnet: { id: privateEndpointSubnetResourceId }
    privateLinkServiceConnections: [{ name: 'table', properties: { privateLinkServiceId: storage.id, groupIds: ['table'] } }]
  }
}
resource tableDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: tablePrivateEndpoint
  name: 'default'
  properties: { privateDnsZoneConfigs: [{ name: 'table', properties: { privateDnsZoneId: tablePrivateDnsZoneResourceId } }] }
}
resource plan 'Microsoft.Web/serverfarms@2024-04-01' = { name: 'plan-runner-broker-${deploymentId}', location: resourceGroup().location, tags: tags, sku: { name: 'FC1', tier: 'FlexConsumption' }, properties: { reserved: true } }
resource broker 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: resourceGroup().location
  tags: tags
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    virtualNetworkSubnetId: brokerIntegrationSubnetResourceId
    clientAffinityEnabled: false
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: false
      appSettings: [
        { name: 'FUNCTIONS_WORKER_RUNTIME_VERSION', value: functionRuntimeVersion }
        { name: 'GITHUB_APP_KEY_SECRET_VERSION_RESOURCE_ID', value: githubAppKeySecretVersionResourceId }
        { name: 'GITHUB_APP_ID', value: githubAppId }
        { name: 'GITHUB_INSTALLATION_ID', value: githubInstallationId }
        { name: 'RUNNER_GROUP_ID', value: string(runnerGroupId) }
        { name: 'BROKER_AUDIENCE', value: brokerAudience }
        { name: 'BROKER_HOSTNAME', value: brokerHostname }
        { name: 'EXISTING_VNET_RESOURCE_ID', value: existingVnetResourceId }
        { name: 'EXISTING_KEY_VAULT_RESOURCE_ID', value: existingKeyVaultResourceId }
        { name: 'MAXIMUM_OPERATION_MINUTES', value: '45' }
      ]
    }
  }
}
output brokerResourceId string = broker.id
output brokerPrincipalId string = broker.identity.principalId
output operationTableResourceId string = operationTable.id
output tablePrivateEndpointResourceId string = tablePrivateEndpoint.id
output requiresSeparateRoleAssignments bool = true
