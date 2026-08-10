using './main.bicep'

param releaseSha = '0000000000000000000000000000000000000000'
param imageDigest = 'sha256:0000000000000000000000000000000000000000000000000000000000000000'
param workforceTenantId = 'd7add2bb-ac03-49a8-9377-d0bf6a012f2f'
param virtualNetworkName = 'vnet-adventures-suite-dev'
param containerAppsSubnetPrefix = '10.40.3.0/27'
param sqlServerFqdn = 'adventures-suite-dev-sql.database.windows.net'
