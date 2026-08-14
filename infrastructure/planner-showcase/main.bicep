targetScope = 'resourceGroup'

@description('Name of the isolated public showcase App Service.')
param appName string

@description('Name of the existing non-production Linux App Service plan.')
param appServicePlanName string

@description('Creator used only to resolve local public image resources.')
param showcaseCreatorId string

@description('Immutable source revision included in health and deployment evidence.')
param commitSha string

@description('Azure region inherited from the existing App Service plan.')
param location string = resourceGroup().location

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' existing = {
  name: appServicePlanName
}

resource showcaseApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  tags: {
    Environment: 'Showcase'
    Purpose: 'Synthetic Planner customer demonstration'
    DataClassification: 'Fictional public data only'
    SourceRevision: commitSha
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    clientAffinityEnabled: false
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      http20Enabled: true
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      linuxFxVersion: ''
      appCommandLine: './TheSimontonAdventures.Web'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Showcase'
        }
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'Authentication__Mode'
          value: 'Disabled'
        }
        {
          name: 'CreatorResolution__AzureDefaultCreatorId'
          value: showcaseCreatorId
        }
        {
          name: 'Showcase__Enabled'
          value: 'true'
        }
        {
          name: 'Deployment__CommitSha'
          value: commitSha
        }
        {
          name: 'Deployment__RunId'
          value: 'manual-showcase-bootstrap'
        }
        {
          name: 'Deployment__RunAttempt'
          value: '1'
        }
        {
          name: 'WEBSITE_HEALTHCHECK_MAXPINGFAILURES'
          value: '3'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'WEBSITES_CONTAINER_START_TIME_LIMIT'
          value: '600'
        }
      ]
    }
  }
}

resource ftpPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: showcaseApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource scmPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2023-12-01' = {
  parent: showcaseApp
  name: 'scm'
  properties: {
    allow: false
  }
}

output hostname string = showcaseApp.properties.defaultHostName
output appResourceId string = showcaseApp.id
output sharedPlanResourceId string = appServicePlan.id
