targetScope = 'resourceGroup'

@description('Deployment region. The network settings resource and delegated subnet must share a region.')
param location string = 'westus2'

@description('Immutable existing development virtual network name.')
param virtualNetworkName string = 'vnet-adventures-suite-dev'

@description('Dedicated GitHub-hosted runner subnet name.')
param subnetName string = 'snet-github-private-sql-migration'

@description('Dedicated GitHub-hosted runner network security group name.')
param networkSecurityGroupName string = 'nsg-github-private-sql-migration'

@description('GitHub.Network network settings resource name.')
param networkSettingsName string = 'private-sql-migration-vnet'

@description('Immutable GitHub organization database/business ID.')
param githubBusinessId string = '316268438'

var subnetAddressPrefix = '10.40.3.0/27'
var sqlPrivateEndpointAddress = '10.40.1.4/32'

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: virtualNetworkName
}

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2024-05-01' = {
  name: networkSecurityGroupName
  location: location
  properties: {
    securityRules: [
      {
        name: 'DenyAllInbound'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 100
          direction: 'Inbound'
        }
      }
      {
        name: 'AllowPrivateSqlOutbound'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '1433'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: sqlPrivateEndpointAddress
          access: 'Allow'
          priority: 220
          direction: 'Outbound'
        }
      }
      {
        name: 'AllowHttpsOutbound'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: 'Internet'
          access: 'Allow'
          priority: 230
          direction: 'Outbound'
        }
      }
      {
        name: 'DenyAllOutbound'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 4000
          direction: 'Outbound'
        }
      }
    ]
  }
}

resource subnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  name: subnetName
  parent: virtualNetwork
  properties: {
    addressPrefix: subnetAddressPrefix
    networkSecurityGroup: {
      id: networkSecurityGroup.id
    }
    delegations: [
      {
        name: 'GitHubNetworkSettingsDelegation'
        properties: {
          serviceName: 'GitHub.Network/networkSettings'
        }
      }
    ]
    privateEndpointNetworkPolicies: 'Enabled'
    privateLinkServiceNetworkPolicies: 'Enabled'
  }
}

resource networkSettings 'GitHub.Network/networkSettings@2024-04-02' = {
  name: networkSettingsName
  location: location
  properties: {
    businessId: githubBusinessId
    subnetId: subnet.id
  }
}

output virtualNetworkResourceId string = virtualNetwork.id
output subnetResourceId string = subnet.id
output networkSecurityGroupResourceId string = networkSecurityGroup.id
output networkSettingsResourceId string = networkSettings.id
output githubNetworkConfigurationId string = networkSettings.tags.GitHubId
output githubNetworkConfigurationName string = networkSettingsName
