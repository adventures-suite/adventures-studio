using './identity-access.bicep'

param githubRepository = 'ssimonton007/adventures-studio'
param publisherIdentityResourceId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-publisher-dev'
param starterIdentityResourceId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-starter-dev'
