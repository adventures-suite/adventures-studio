targetScope = 'resourceGroup'

param vnetName string
param subnetName string
param networkSecurityGroupResourceId string

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: vnetName
}

resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  name: subnetName
  parent: vnet
  properties: {
    addressPrefix: '10.40.3.0/27'
    networkSecurityGroup: { id: networkSecurityGroupResourceId }
    privateEndpointNetworkPolicies: 'Enabled'
    privateLinkServiceNetworkPolicies: 'Enabled'
  }
}

output subnetResourceId string = subnet.id
