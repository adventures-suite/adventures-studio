targetScope = 'resourceGroup'

@description('Exact reviewed repository permitted to federate with GitHub Actions.')
param githubRepository string = 'ssimonton007/adventures-studio'
param publisherIdentityResourceId string
param starterIdentityResourceId string

resource publisherIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-publisher-dev' }
resource starterIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = { name: 'id-adventures-suite-migrate-starter-dev' }

var validatedGithubRepository = githubRepository == 'ssimonton007/adventures-studio' ? githubRepository : fail('The GitHub repository is not approved.')
var validatedPublisherIdentityResourceId = toLower(publisherIdentityResourceId) == toLower(publisherIdentity.id) ? publisherIdentityResourceId : fail('The publisher identity resource ID is not approved.')
var validatedStarterIdentityResourceId = toLower(starterIdentityResourceId) == toLower(starterIdentity.id) ? starterIdentityResourceId : fail('The starter identity resource ID is not approved.')
var environmentSubject = 'repo:${validatedGithubRepository}:environment:database-development'

resource publisherFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: publisherIdentity
  name: 'github-database-development'
  properties: { issuer: 'https://token.actions.githubusercontent.com', subject: environmentSubject, audiences: ['api://AzureADTokenExchange'] }
}

resource starterFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: starterIdentity
  name: 'github-database-development'
  properties: { issuer: 'https://token.actions.githubusercontent.com', subject: environmentSubject, audiences: ['api://AzureADTokenExchange'] }
}

output validatedPublisherIdentityResourceId string = validatedPublisherIdentityResourceId
output validatedStarterIdentityResourceId string = validatedStarterIdentityResourceId
