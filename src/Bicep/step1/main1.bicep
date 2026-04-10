@description('Display Name of the database administrators group from Entra ID. The user running this wizard needs to be in this group.')
param applicationDatabaseAdminsGroupName string

@description('Object ID of the database administrators group from Entra ID. The user running this wizard needs to be in this group.')
param applicationDatabaseAdminsObjectId string 


@allowed([
  'dev'
  'prd'
])
param deployEnvironment string = 'dev'

@description('Location for all resources.')
param location string = resourceGroup().location

param appName string

param adminUsername string = ''
@secure()
param adminDcPassword string = ''
@secure()
param adminWorkerPassword string = ''

param domainControllerName string = 'dc01'
param domainFQDN string
param sleepSeconds int = 60

param EnterpriseAppTenantDomain string = 'kurcontoso.onmicrosoft.com'
param EnterpriseAppTenantId string = tenant().tenantId
param EnterpriseAppClientId string

@description('Client ID of the ShieldChecker-BackendApi app registration (WebApp.Access AppRole).')
param backendApiAppClientId string

@description('Client ID of the ShieldChecker-HostServiceApi app registration (HostService.Access AppRole).')
param hostServiceApiAppClientId string

@description('Client ID of the ShieldChecker-ImportApi app registration (Import.SharedLibrary AppRole).')
param importApiAppClientId string

@description('Image tag for the WebApp container image in ACR.')
param webAppImageTag string = 'latest'

@description('Image tag for the BackendApi container image in ACR.')
param backendApiImageTag string = 'latest'

@description('Image tag for the HostServiceApi container image in ACR.')
param hostServiceApiImageTag string = 'latest'

@description('Image tag for the ImportApi container image in ACR.')
param importApiImageTag string = 'latest'

@description('Deployment module of network')
module deployment_network 'resource.network.bicep' = {
  name: 'module.network'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    domainControllerName: domainControllerName
  }
}
@description('Deployment module of identity for app id')
module deployment_identity_app 'resource.identity.app.bicep' = {
  name: 'module.identity.app'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    sleepSeconds: sleepSeconds
  }
}
@description('Deployment module of identity for db id')
module deployment_identity_db 'resource.identity.db.bicep' = {
  name: 'module.identity.db'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    sleepSeconds: sleepSeconds
  }
}
@description('Deployment module of key vault')
module deployment_kv 'resource.kv.bicep' = {
  name: 'module.kv'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    applicationIdentityPrincipalId: deployment_identity_app.outputs.applicationIdentityPrincipalId
    appName: appName
  }
}

@description('Deployment module of Azure SQL')
module deployment_sql 'resource.sql.bicep' = {
  name: 'module.sql'
  params: {
    applicationDatabaseAdminsGroupName: applicationDatabaseAdminsGroupName
    applicationDatabaseAdminsObjectId: applicationDatabaseAdminsObjectId
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    subnetManagementResourceId: deployment_network.outputs.subnetManagementResourceId
    dbIdentityId: deployment_identity_db.outputs.dbIdentityId
    kvName: deployment_kv.outputs.kvName
  }
}

@description('Deployment module of Azure Container Registry')
module deployment_acr 'resource.acr.bicep' = {
  name: 'module.acr'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    acrPullPrincipalId: deployment_identity_app.outputs.applicationIdentityPrincipalId
  }
}

@description('Deployment module of Log Analytics workspace (used by Container Apps environment)')
module deployment_logs 'resource.logs.bicep' = {
  name: 'module.logs'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
  }
}

@description('Deployment module of Container Apps environment')
module deployment_containerenv 'resource.containerenv.bicep' = {
  name: 'module.containerenv'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    subnetResourceId: deployment_network.outputs.subnetManagementResourceId
    logAnalyticsWorkspaceId: deployment_logs.outputs.workspaceId
    logAnalyticsWorkspaceCustomerId: deployment_logs.outputs.workspaceCustomerId
    logAnalyticsWorkspaceSharedKey: deployment_logs.outputs.workspaceSharedKey
  }
}

@description('Deployment module of web components (Container Apps)')
module deployment_web 'resource.web.bicep' = {
  name: 'module.web'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    subnetManagementResourceId: deployment_network.outputs.subnetManagementResourceId
    subnetWorkerResourceId: deployment_network.outputs.subnetWorkerResourceId
    subnetDcResourceId: deployment_network.outputs.subnetDcResourceId
    sqlServerDatabaseName: deployment_sql.outputs.sqlServerDatabaseName
    sqlServerName: deployment_sql.outputs.sqlServerName
    applicationIdentityClientId: deployment_identity_app.outputs.applicationIdentityClientId
    applicationIdentityId: deployment_identity_app.outputs.applicationIdentityId
    applicationIdentityPrincipalId: deployment_identity_app.outputs.applicationIdentityPrincipalId
    kvUrl: deployment_kv.outputs.kvUrl
    kvName: deployment_kv.outputs.kvName
    EnterpriseAppClientId: EnterpriseAppClientId
    EnterpriseAppTenantDomain: EnterpriseAppTenantDomain
    EnterpriseAppTenantId: EnterpriseAppTenantId
    domainFQDN: domainFQDN
    domainControllerName: domainControllerName
    storageAccountDCRName: ''
    containerAppsEnvironmentId: deployment_containerenv.outputs.containerAppsEnvironmentId
    containerRegistryLoginServer: deployment_acr.outputs.containerRegistryLoginServer
    appInsightsInstrumentationKey: deployment_logs.outputs.appInsightsInstrumentationKey
    webAppImageTag: webAppImageTag
    backendApiImageTag: backendApiImageTag
    hostServiceApiImageTag: hostServiceApiImageTag
    importApiImageTag: importApiImageTag
    backendApiAppClientId: backendApiAppClientId
    hostServiceApiAppClientId: hostServiceApiAppClientId
    importApiAppClientId: importApiAppClientId
  }
}



output sqlServerName string = deployment_sql.outputs.sqlServerName
output sqlServerDatabaseName string = deployment_sql.outputs.sqlServerDatabaseName
output subnetWorkerNetworkResourceId string = deployment_network.outputs.subnetWorkerResourceId
output subnetManagementNetworkResourceId string = deployment_network.outputs.subnetManagementResourceId
output subnetDcNetworkResourceId string = deployment_network.outputs.subnetDcResourceId
output sqlConnectionString string = deployment_sql.outputs.sqlConnectionString
output applicationIdentityName string = deployment_identity_app.outputs.applicationIdentityName
output webAppName string = deployment_web.outputs.webAppName
output webAppFqdn string = deployment_web.outputs.webAppFqdn
output backendApiAppName string = deployment_web.outputs.backendApiAppName
output backendApiAppFqdn string = deployment_web.outputs.backendApiAppFqdn
output hostServiceApiAppName string = deployment_web.outputs.hostServiceApiAppName
output hostServiceApiAppFqdn string = deployment_web.outputs.hostServiceApiAppFqdn
output importApiAppName string = deployment_web.outputs.importApiAppName
output importApiAppFqdn string = deployment_web.outputs.importApiAppFqdn
// Kept for backward compatibility
output apiAppName string = deployment_web.outputs.apiAppName
output apiAppFqdn string = deployment_web.outputs.apiAppFqdn
output functionAppHostname string = deployment_web.outputs.functionAppHostname
output containerRegistryName string = deployment_acr.outputs.containerRegistryName
output containerRegistryLoginServer string = deployment_acr.outputs.containerRegistryLoginServer
