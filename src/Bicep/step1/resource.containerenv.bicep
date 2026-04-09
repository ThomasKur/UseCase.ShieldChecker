@allowed([
  'dev'
  'prd'
])
@description('Deployment environment.')
param deployEnvironment string = 'dev'

@description('Location for all resources.')
param location string

@description('Name of the application.')
param appName string

@description('Resource ID of the subnet used for VNet integration of the Container Apps environment.')
param subnetResourceId string

@description('Resource ID of the Log Analytics workspace for diagnostics.')
param logAnalyticsWorkspaceId string

@description('Customer ID (workspace ID) of the Log Analytics workspace.')
param logAnalyticsWorkspaceCustomerId string

@description('Shared key of the Log Analytics workspace.')
@secure()
param logAnalyticsWorkspaceSharedKey string

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${appName}-${location}-${deployEnvironment}-001'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspaceCustomerId
        sharedKey: logAnalyticsWorkspaceSharedKey
      }
    }
    vnetConfiguration: {
      infrastructureSubnetId: subnetResourceId
      internal: false
    }
    zoneRedundant: false
  }
}

output containerAppsEnvironmentId string = containerAppsEnvironment.id
output containerAppsEnvironmentName string = containerAppsEnvironment.name
output containerAppsEnvironmentDefaultDomain string = containerAppsEnvironment.properties.defaultDomain
