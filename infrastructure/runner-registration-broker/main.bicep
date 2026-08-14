targetScope = 'resourceGroup'

@description('Exact existing development VNet resource ID; no default.')
param existingVnetResourceId string

@description('Exact existing private-endpoint subnet resource ID; no default.')
param privateEndpointSubnetResourceId string

@description('Exact existing privatelink.vaultcore.azure.net private DNS zone resource ID; no default.')
param vaultPrivateDnsZoneResourceId string

@description('Exact existing privatelink.blob.core.windows.net private DNS zone resource ID; no default.')
param blobPrivateDnsZoneResourceId string

@description('Bounded Log Analytics ingestion cap in GB/day; no live-discovery value is defaulted.')
@minValue(1)
@maxValue(1)
param logAnalyticsDailyCapGb int

var location = 'westus2'
var functionName = 'func-adventures-suite-runner-broker-dev'
var planName = 'plan-adventures-suite-runner-broker-dev'
var storageName = 'stadvsrunnerbrokerdev'
var vaultName = 'kv-adventures-runner-dev'
var workspaceName = 'log-adventures-suite-runner-broker-dev'
var appInsightsName = 'appi-adventures-suite-runner-broker-dev'
var deploymentContainerName = 'function-releases'
var operationTableName = 'RunnerOperations'
var integrationSubnetName = 'snet-runner-broker-integration'
var tablePrivateDnsZoneName = 'privatelink.table.${environment().suffixes.storage}'
var queuePrivateDnsZoneName = 'privatelink.queue.${environment().suffixes.storage}'
var tags = {
  purpose: 'ephemeral-runner-registration-broker'
  environment: 'development'
  persistentCompute: 'false'
  containsCredentials: 'false'
}

resource existingVnet 'Microsoft.Network/virtualNetworks@2024-05-01' existing = {
  name: last(split(existingVnetResourceId, '/'))
}

resource integrationSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-05-01' = {
  parent: existingVnet
  name: integrationSubnetName
  properties: {
    addressPrefix: '10.40.4.0/27'
    delegations: [
      {
        name: 'flex-consumption'
        properties: {
          serviceName: 'Microsoft.App/environments'
        }
      }
    ]
    privateEndpointNetworkPolicies: 'Enabled'
    privateLinkServiceNetworkPolicies: 'Enabled'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Deny'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: deploymentContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource operationTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: operationTableName
}

resource tablePrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: tablePrivateDnsZoneName
  location: 'global'
  tags: tags
}

resource tableDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: tablePrivateDnsZone
  name: 'link-adventures-suite-dev-table'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: existingVnetResourceId
    }
  }
}

resource queuePrivateDnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: queuePrivateDnsZoneName
  location: 'global'
  tags: tags
}

resource queueDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: queuePrivateDnsZone
  name: 'link-adventures-suite-dev-queue'
  location: 'global'
  tags: tags
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: existingVnetResourceId
    }
  }
}

resource blobPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-adventures-runner-blob-dev'
  location: location
  tags: tags
  properties: {
    subnet: { id: privateEndpointSubnetResourceId }
    privateLinkServiceConnections: [
      {
        name: 'blob'
        properties: {
          privateLinkServiceId: storage.id
          groupIds: [ 'blob' ]
        }
      }
    ]
  }
}

resource blobDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: blobPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'blob'
        properties: { privateDnsZoneId: blobPrivateDnsZoneResourceId }
      }
    ]
  }
}

resource queuePrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-adventures-runner-queue-dev'
  location: location
  tags: tags
  properties: {
    subnet: { id: privateEndpointSubnetResourceId }
    privateLinkServiceConnections: [
      {
        name: 'queue'
        properties: {
          privateLinkServiceId: storage.id
          groupIds: [ 'queue' ]
        }
      }
    ]
  }
}

resource queueDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: queuePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'queue'
        properties: { privateDnsZoneId: queuePrivateDnsZone.id }
      }
    ]
  }
}

resource tablePrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-adventures-runner-table-dev'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetResourceId
    }
    privateLinkServiceConnections: [
      {
        name: 'table'
        properties: {
          privateLinkServiceId: storage.id
          groupIds: [
            'table'
          ]
        }
      }
    ]
  }
}

resource tableDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: tablePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'table'
        properties: {
          privateDnsZoneId: tablePrivateDnsZone.id
        }
      }
    ]
  }
}

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'None'
      defaultAction: 'Deny'
    }
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
  }
}

resource vaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-adventures-runner-kv-dev'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetResourceId
    }
    privateLinkServiceConnections: [
      {
        name: 'vault'
        properties: {
          privateLinkServiceId: vault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource vaultDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: vaultPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vault'
        properties: {
          privateDnsZoneId: vaultPrivateDnsZoneResourceId
        }
      }
    ]
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    workspaceCapping: {
      dailyQuotaGb: logAnalyticsDailyCapGb
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    RetentionInDays: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource broker 'Microsoft.Web/sites@2024-11-01' = {
  name: functionName
  location: location
  tags: tags
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    virtualNetworkSubnetId: integrationSubnet.id
    clientAffinityEnabled: false
    outboundVnetRouting: {
      allTraffic: true
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      runtime: {
        name: 'node'
        version: '24'
      }
      scaleAndConcurrency: {
        instanceMemoryMB: 512
        maximumInstanceCount: 1
        alwaysReady: []
        triggers: {
          http: {
            perInstanceConcurrency: 1
          }
        }
      }
    }
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: false
      http20Enabled: true
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storage.name
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'
          value: 'Authorization=AAD'
        }
        {
          name: 'BROKER_TELEMETRY_MODE'
          value: 'bounded-redacted'
        }
        {
          name: 'MAXIMUM_OPERATION_MINUTES'
          value: '45'
        }
      ]
    }
  }
}

output brokerResourceId string = broker.id
output brokerPrincipalId string = broker.identity.principalId
output brokerTenantId string = broker.identity.tenantId
output brokerHostname string = broker.properties.defaultHostName
output integrationSubnetResourceId string = integrationSubnet.id
output storageResourceId string = storage.id
output deploymentContainerResourceId string = deploymentContainer.id
output operationTableResourceId string = operationTable.id
output vaultResourceId string = vault.id
output workspaceResourceId string = workspace.id
output appInsightsResourceId string = appInsights.id
output tablePrivateEndpointResourceId string = tablePrivateEndpoint.id
output blobPrivateEndpointResourceId string = blobPrivateEndpoint.id
output queuePrivateEndpointResourceId string = queuePrivateEndpoint.id
output vaultPrivateEndpointResourceId string = vaultPrivateEndpoint.id
output requiresSeparateRoleAssignments bool = true
output containsCredentialMaterial bool = false
