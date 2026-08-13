targetScope = 'resourceGroup'
@minLength(8)
@maxLength(32)
param operationId string
param existingVnetResourceId string
param migrationIdentityResourceId string
param adminUsername string
@secure()
param ephemeralAdminSshPublicKey string
@allowed(['24.04.202608070'])
param ubuntuImageVersion string = '24.04.202608070'
@allowed(['Standard_B2als_v2'])
param vmSize string = 'Standard_B2als_v2'
@secure()
param bootstrapCustomData string
var vnetName = last(split(existingVnetResourceId, '/'))
var identityName = last(split(migrationIdentityResourceId, '/'))
var tags = { purpose: 'one-job-private-migration-runner', operationId: operationId, expiresAfterMinutes: '45' }
resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = { name: vnetName }
resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: identityName }
resource nsg 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: 'nsg-migration-runner-${operationId}'
  location: resourceGroup().location
  tags: tags
  properties: { securityRules: [
    { name: 'DenyAllInbound', properties: { priority: 100, direction: 'Inbound', access: 'Deny', protocol: '*', sourcePortRange: '*', destinationPortRange: '*', sourceAddressPrefix: '*', destinationAddressPrefix: '*' } }
    { name: 'AllowPrivateSql', properties: { priority: 100, direction: 'Outbound', access: 'Allow', protocol: 'Tcp', sourcePortRange: '*', destinationPortRange: '1433', sourceAddressPrefix: '*', destinationAddressPrefix: '10.40.1.4/32' } }
    { name: 'AllowHttpsBootstrap', properties: { priority: 110, direction: 'Outbound', access: 'Allow', protocol: 'Tcp', sourcePortRange: '*', destinationPortRange: '443', sourceAddressPrefix: '*', destinationAddressPrefix: 'Internet', description: 'Guest nftables restricts HTTPS to reviewed GitHub and Sigstore destinations.' } }
    { name: 'DenyAllOtherOutbound', properties: { priority: 200, direction: 'Outbound', access: 'Deny', protocol: '*', sourcePortRange: '*', destinationPortRange: '*', sourceAddressPrefix: '*', destinationAddressPrefix: '*' } }
  ] }
}
resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = { name: 'snet-migration-runner-${operationId}', parent: vnet, properties: { addressPrefix: '10.40.3.0/27', networkSecurityGroup: { id: nsg.id }, privateEndpointNetworkPolicies: 'Enabled', privateLinkServiceNetworkPolicies: 'Enabled' } }
resource nic 'Microsoft.Network/networkInterfaces@2024-05-01' = { name: 'nic-migration-runner-${operationId}', location: resourceGroup().location, tags: tags, properties: { enableAcceleratedNetworking: false, enableIPForwarding: false, ipConfigurations: [{ name: 'ipconfig1', properties: { privateIPAllocationMethod: 'Dynamic', subnet: { id: subnet.id } } }] } }
resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: 'vm-migration-runner-${operationId}'
  location: resourceGroup().location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${migrationIdentity.id}': {} } }
  properties: {
    hardwareProfile: { vmSize: vmSize }
    securityProfile: { securityType: 'TrustedLaunch', uefiSettings: { secureBootEnabled: true, vTpmEnabled: true } }
    storageProfile: { imageReference: { publisher: 'Canonical', offer: 'ubuntu-24_04-lts', sku: 'server', version: ubuntuImageVersion }, osDisk: { name: 'disk-migration-runner-${operationId}-os', createOption: 'FromImage', deleteOption: 'Delete', diskSizeGB: 32, managedDisk: { storageAccountType: 'StandardSSD_LRS' } }, dataDisks: [] }
    osProfile: { computerName: 'vm-migration-runner-${operationId}', adminUsername: adminUsername, customData: bootstrapCustomData, linuxConfiguration: { disablePasswordAuthentication: true, provisionVMAgent: true, ssh: { publicKeys: [{ path: '/home/${adminUsername}/.ssh/authorized_keys', keyData: ephemeralAdminSshPublicKey }] }, patchSettings: { patchMode: 'ImageDefault', assessmentMode: 'ImageDefault' } }, allowExtensionOperations: false }
    networkProfile: { networkInterfaces: [{ id: nic.id, properties: { deleteOption: 'Delete', primary: true } }] }
    diagnosticsProfile: { bootDiagnostics: { enabled: false } }
  }
}
output operationId string = operationId
output resourceIds array = [vm.id, nic.id, vm.properties.storageProfile.osDisk.name, nsg.id, subnet.id]
