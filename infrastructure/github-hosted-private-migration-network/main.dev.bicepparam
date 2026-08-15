using './main.bicep'

param location = 'westus2'
param virtualNetworkName = 'vnet-adventures-suite-dev'
param subnetName = 'snet-github-private-sql-migration'
param networkSecurityGroupName = 'nsg-github-private-sql-migration'
param networkSettingsName = 'private-sql-migration-vnet'
param githubBusinessId = '316268438'
