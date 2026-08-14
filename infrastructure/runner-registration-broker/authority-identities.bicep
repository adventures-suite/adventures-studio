targetScope = 'resourceGroup'

@description('Exact approved Azure region; no default.')
param location string

var tags = {
  purpose: 'runner-broker-foundation-authority'
  environment: 'development'
  containsCredentials: 'false'
  liveAuthority: 'false-until-separately-assigned'
}

resource provisioner 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-adventures-suite-runner-broker-foundation-deployer-dev'
  location: location
  tags: tags
}

resource cleanup 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-adventures-suite-runner-broker-foundation-cleanup-dev'
  location: location
  tags: tags
}

resource residueReader 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'id-adventures-suite-runner-broker-foundation-residue-reader-dev'
  location: location
  tags: tags
}

output provisionerResourceId string = provisioner.id
output provisionerClientId string = provisioner.properties.clientId
output provisionerPrincipalId string = provisioner.properties.principalId
output cleanupResourceId string = cleanup.id
output cleanupClientId string = cleanup.properties.clientId
output cleanupPrincipalId string = cleanup.properties.principalId
output residueReaderResourceId string = residueReader.id
output residueReaderClientId string = residueReader.properties.clientId
output residueReaderPrincipalId string = residueReader.properties.principalId
