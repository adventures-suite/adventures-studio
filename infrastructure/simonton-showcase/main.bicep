targetScope = 'resourceGroup'

@description('Name of The Simonton Adventures public showcase App Service.')
param appName string

@description('Name of the existing non-production Linux App Service plan.')
param appServicePlanName string

@description('Creator whose approved public content is presented.')
param creatorId string

@description('Immutable source revision included in health and deployment evidence.')
param commitSha string

@description('Object ID of the GitHub dev-environment deployment principal.')
param deploymentPrincipalObjectId string

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
    Environment: 'PublicShowcase'
    Purpose: 'The Simonton Adventures public story'
    DataClassification: 'Approved public Creator content'
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
      linuxFxVersion: 'DOTNETCORE|10.0'
      appCommandLine: './TheSimontonAdventures.Web'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'PublicShowcase'
        }
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'ASPNETCORE_URLS'
          value: 'http://0.0.0.0:8080'
        }
        {
          name: 'Authentication__Mode'
          value: 'Disabled'
        }
        {
          name: 'CreatorResolution__AzureDefaultCreatorId'
          value: creatorId
        }
        {
          name: 'Showcase__Enabled'
          value: 'false'
        }
        {
          name: 'Preview__NoIndex'
          value: 'true'
        }
        {
          name: 'Deployment__CommitSha'
          value: commitSha
        }
        {
          name: 'Deployment__RunId'
          value: 'manual-public-showcase-bootstrap'
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
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
      ]
    }
  }
}

resource deploymentAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(showcaseApp.id, deploymentPrincipalObjectId, 'de139f84-1756-47ae-9be6-808fbbe84772')
  scope: showcaseApp
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'de139f84-1756-47ae-9be6-808fbbe84772')
    principalId: deploymentPrincipalObjectId
    principalType: 'ServicePrincipal'
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
