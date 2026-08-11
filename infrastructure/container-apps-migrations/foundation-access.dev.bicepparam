using './foundation-access.bicep'

param registryResourceId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ContainerRegistry/registries/advsuitemigrationsdev'
param logWorkspaceResourceId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.OperationalInsights/workspaces/log-adventures-suite-migrations-dev'
param pullIdentityResourceId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-pull-dev'
param publisherIdentityResourceId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-publisher-dev'
param starterIdentityResourceId = '/subscriptions/5ace9cdd-06d1-47d9-8214-1e7c756d076a/resourceGroups/rg-adventures-suite-dev/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-adventures-suite-migrate-starter-dev'
