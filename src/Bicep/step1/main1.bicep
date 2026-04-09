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

param adminUsername string
@secure()
param adminDcPassword string
@secure()
param adminWorkerPassword string

param domainControllerName string = 'dc01'
param domainFQDN string
param sleepSeconds int = 60

param EnterpriseAppTenantDomain string = 'kurcontoso.onmicrosoft.com'
param EnterpriseAppTenantId string = tenant().tenantId
param EnterpriseAppClientId string

@description('Image tag for the WebApp container image in ACR.')
param webAppImageTag string = 'latest'

@description('Image tag for the API container image in ACR.')
param apiImageTag string = 'latest'

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
@description('Deployment module of identity for vmdc id')
module deployment_identity_vmdc 'resource.identity.vmdc.bicep' = {
  name: 'module.identity.vmdc'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
  }
}

@description('Deployment module of storage account')
module deployment_storage 'resource.storage.bicep' = {
  name: 'module.storage'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    appName: appName
    subnetManagementResourceId: deployment_network.outputs.subnetManagementResourceId
  }
}

@description('Deployment module of key vault')
module deployment_kv 'resource.kv.bicep' = {
  name: 'module.kv'
  params: {
    deployEnvironment: deployEnvironment
    location: location
    applicationIdentityPrincipalId: deployment_identity_app.outputs.applicationIdentityPrincipalId
    adminDcPassword: adminDcPassword
    adminWorkerPassword: adminWorkerPassword
    adminUsername: adminUsername
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
    storageAccountDCRName: deployment_storage.outputs.storageAccountDCRName
    containerAppsEnvironmentId: deployment_containerenv.outputs.containerAppsEnvironmentId
    containerRegistryLoginServer: deployment_acr.outputs.containerRegistryLoginServer
    appInsightsInstrumentationKey: deployment_logs.outputs.appInsightsInstrumentationKey
    webAppImageTag: webAppImageTag
    apiImageTag: apiImageTag
  }
}



output sqlServerName string = deployment_sql.outputs.sqlServerName
output sqlServerDatabaseName string = deployment_sql.outputs.sqlServerDatabaseName
output subnetWorkerNetworkResourceId string = deployment_network.outputs.subnetWorkerResourceId
output subnetManagementNetworkResourceId string = deployment_network.outputs.subnetManagementResourceId
output subnetDcNetworkResourceId string = deployment_network.outputs.subnetDcResourceId
output storageAccountName string = deployment_storage.outputs.storageAccountDCRName
output sqlConnectionString string = deployment_sql.outputs.sqlConnectionString
output applicationIdentityName string = deployment_identity_app.outputs.applicationIdentityName
output vmDcIdentityName string = deployment_identity_vmdc.outputs.vmDcIdentityName
output webAppName string = deployment_web.outputs.webAppName
output functionAppHostname string = deployment_web.outputs.functionAppHostname
output containerRegistryName string = deployment_acr.outputs.containerRegistryName
output containerRegistryLoginServer string = deployment_acr.outputs.containerRegistryLoginServer
