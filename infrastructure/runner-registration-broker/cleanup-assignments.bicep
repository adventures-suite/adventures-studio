targetScope = 'resourceGroup'

@description('Exact post-creation cleanup principal ID; no default.')
param cleanupPrincipalId string
@description('Exact existing VNet name from approved readback; no default.')
param existingVnetName string

var roleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '927117fa-ab5d-42a2-b39e-762663171fa4')
resource broker 'Microsoft.Web/sites@2024-11-01' existing = { name: 'func-adventures-suite-runner-broker-dev' }
resource plan 'Microsoft.Web/serverfarms@2024-04-01' existing = { name: 'plan-adventures-suite-runner-broker-dev' }
resource insights 'Microsoft.Insights/components@2020-02-02' existing = { name: 'appi-adventures-suite-runner-broker-dev' }
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = { name: 'log-adventures-suite-runner-broker-dev' }
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = { name: 'stadvsrunnerbrokerdev' }
resource vault 'Microsoft.KeyVault/vaults@2024-11-01' existing = { name: 'kv-adventures-runner-dev' }
resource peBlob 'Microsoft.Network/privateEndpoints@2024-05-01' existing = { name: 'pe-adventures-runner-blob-dev' }
resource peQueue 'Microsoft.Network/privateEndpoints@2024-05-01' existing = { name: 'pe-adventures-runner-queue-dev' }
resource peTable 'Microsoft.Network/privateEndpoints@2024-05-01' existing = { name: 'pe-adventures-runner-table-dev' }
resource peVault 'Microsoft.Network/privateEndpoints@2024-05-01' existing = { name: 'pe-adventures-runner-kv-dev' }
resource queueDns 'Microsoft.Network/privateDnsZones@2024-06-01' existing = { name: 'privatelink.queue.${environment().suffixes.storage}' }
resource tableDns 'Microsoft.Network/privateDnsZones@2024-06-01' existing = { name: 'privatelink.table.${environment().suffixes.storage}' }
resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = { name: existingVnetName }
resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' existing = { parent: vnet, name: 'snet-runner-broker-integration' }

resource brokerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: broker, name: guid(broker.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource planAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: plan, name: guid(plan.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource insightsAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: insights, name: guid(insights.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource workspaceAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: workspace, name: guid(workspace.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource storageAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: storage, name: guid(storage.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource vaultAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: vault, name: guid(vault.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource peBlobAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: peBlob, name: guid(peBlob.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource peQueueAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: peQueue, name: guid(peQueue.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource peTableAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: peTable, name: guid(peTable.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource peVaultAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: peVault, name: guid(peVault.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource queueDnsAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: queueDns, name: guid(queueDns.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource tableDnsAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: tableDns, name: guid(tableDns.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }
resource subnetAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = { scope: subnet, name: guid(subnet.id, cleanupPrincipalId, roleId), properties: { principalId: cleanupPrincipalId, principalType: 'ServicePrincipal', roleDefinitionId: roleId } }

output assignmentIds array = [brokerAssignment.id, planAssignment.id, insightsAssignment.id, workspaceAssignment.id, storageAssignment.id, vaultAssignment.id, peBlobAssignment.id, peQueueAssignment.id, peTableAssignment.id, peVaultAssignment.id, queueDnsAssignment.id, tableDnsAssignment.id, subnetAssignment.id]
