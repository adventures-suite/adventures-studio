targetScope = 'resourceGroup'

@description('Existing App Service plan resource ID shared by the preview apps.')
param appServicePlanResourceId string

@description('Azure web app name for the public AdventuresSuite preview.')
param platformPreviewAppName string

@description('Azure web app name for The Simonton Adventures preview.')
param creatorPreviewAppName string

@description('Canonical HTTPS sign-in endpoint on the existing Creator workspace.')
param workspaceSignInUrl string

@description('Object ID of the GitHub dev-environment deployment service principal.')
param deploymentPrincipalObjectId string

var websiteContributorRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'de139f84-1756-47ae-9be6-808fbbe84772')

resource platformPreview 'Microsoft.Web/sites@2024-04-01' = {
  name: platformPreviewAppName
  location: resourceGroup().location
  kind: 'app,linux'
  tags: {
    environment: 'development'
    component: 'public-platform-preview'
    managedBy: 'bicep'
  }
  properties: {
    serverFarmId: appServicePlanResourceId
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: false
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
    }
  }
}

resource creatorPreview 'Microsoft.Web/sites@2024-04-01' = {
  name: creatorPreviewAppName
  location: resourceGroup().location
  kind: 'app,linux'
  tags: {
    environment: 'development'
    component: 'creator-experience-preview'
    managedBy: 'bicep'
  }
  properties: {
    serverFarmId: appServicePlanResourceId
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: false
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      healthCheckPath: '/health'
    }
  }
}

resource platformPreviewSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  name: 'appsettings'
  parent: platformPreview
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Production'
    Authentication__Mode: 'Disabled'
    PlatformHosts__Hosts__0: platformPreview.properties.defaultHostName
    PlatformHosts__FeaturedAdventureUrl: 'https://${creatorPreview.properties.defaultHostName}/'
    PlatformHosts__WorkspaceSignInUrl: workspaceSignInUrl
    Preview__NoIndex: 'true'
    WEBSITE_HEALTHCHECK_MAXPINGFAILURES: '3'
    WEBSITES_CONTAINER_START_TIME_LIMIT: '600'
    WEBSITE_RUN_FROM_PACKAGE: '1'
  }
}

resource creatorPreviewSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  name: 'appsettings'
  parent: creatorPreview
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Production'
    Authentication__Mode: 'Disabled'
    CreatorResolution__AzureDefaultCreatorId: 'creator_tsa_01'
    Preview__NoIndex: 'true'
    WEBSITE_HEALTHCHECK_MAXPINGFAILURES: '3'
    WEBSITES_CONTAINER_START_TIME_LIMIT: '600'
    WEBSITE_RUN_FROM_PACKAGE: '1'
  }
}

resource platformDeploymentAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(platformPreview.id, deploymentPrincipalObjectId, websiteContributorRoleDefinitionId)
  scope: platformPreview
  properties: {
    roleDefinitionId: websiteContributorRoleDefinitionId
    principalId: deploymentPrincipalObjectId
    principalType: 'ServicePrincipal'
  }
}

resource creatorDeploymentAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(creatorPreview.id, deploymentPrincipalObjectId, websiteContributorRoleDefinitionId)
  scope: creatorPreview
  properties: {
    roleDefinitionId: websiteContributorRoleDefinitionId
    principalId: deploymentPrincipalObjectId
    principalType: 'ServicePrincipal'
  }
}

output platformPreviewAppName string = platformPreview.name
output platformPreviewHostname string = platformPreview.properties.defaultHostName
output creatorPreviewAppName string = creatorPreview.name
output creatorPreviewHostname string = creatorPreview.properties.defaultHostName
