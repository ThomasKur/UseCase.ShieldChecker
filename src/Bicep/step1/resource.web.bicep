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

param domainFQDN string
param domainControllerName string

param storageAccountDCRName string = ''

param apiAppClientId string

param applicationIdentityClientId string
param applicationIdentityId string
param applicationIdentityPrincipalId string
param kvUrl string
param kvName string

param subnetManagementResourceId string
param subnetWorkerResourceId string
param subnetDcResourceId string

param sqlServerName string
param sqlServerDatabaseName string

param EnterpriseAppTenantDomain string
param EnterpriseAppTenantId string
param EnterpriseAppClientId string

param containerAppsEnvironmentId string

@description('Login server of the Azure Container Registry (e.g. crfoo.azurecr.io).')
param containerRegistryLoginServer string

@description('App Insights instrumentation key (provided by resource.logs.bicep).')
param appInsightsInstrumentationKey string

@description('Image tag for the WebApp container.')
param webAppImageTag string = 'latest'

@description('Image tag for the API (Function App equivalent) container.')
param apiImageTag string = 'latest'

var sqlConnectionString = 'Server=tcp:${sqlServerName},1433;Initial Catalog=${sqlServerDatabaseName};Authentication=Active Directory Default;Encrypt=True;MultipleActiveResultSets=True;'

// ── WebApp Container App ──────────────────────────────────────────────────────

resource webApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${appName}-web-${deployEnvironment}-001'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${applicationIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: applicationIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'webapp'
          image: '${containerRegistryLoginServer}/shieldchecker-webapp:${webAppImageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'AzureSqlDatabase', value: sqlConnectionString }
            { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsightsInstrumentationKey }
            { name: 'AZURE_TENANT_ID', value: tenant().tenantId }
            { name: 'AZURE_CLIENT_ID', value: applicationIdentityClientId }
            { name: 'KEYVAULT_URI', value: kvUrl }
            { name: 'AzureAd__Instance', value: environment().authentication.loginEndpoint }
            { name: 'AzureAd__Domain', value: EnterpriseAppTenantDomain }
            { name: 'AzureAd__TenantId', value: EnterpriseAppTenantId }
            { name: 'AzureAd__ClientId', value: EnterpriseAppClientId }
            { name: 'AzureAd__CallbackPath', value: '/signin-oidc' }
            { name: 'MicrosoftGraph__BaseUrl', value: 'https://graph.microsoft.com' }
            { name: 'MicrosoftGraph__Version', value: 'v1.0' }
            { name: 'MicrosoftGraph__Scopes__0', value: 'user.read' }
            { name: 'SC_FUN_HOSTNAME', value: apiApp.properties.configuration.ingress.fqdn }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// ── API Container App (replaces Azure Function App) ───────────────────────────
// The API container uses the same user-assigned managed identity as the WebApp to
// connect to Azure SQL using Active Directory Default authentication — no password
// or connection string secret is required.

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${appName}-api-${deployEnvironment}-001'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${applicationIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: containerRegistryLoginServer
          identity: applicationIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${containerRegistryLoginServer}/shieldchecker-api:${apiImageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            // Managed identity credentials — DefaultAzureCredential picks these up
            // automatically so the container connects to SQL without a password.
            { name: 'AZURE_CLIENT_ID', value: applicationIdentityClientId }
            { name: 'AZURE_TENANT_ID', value: tenant().tenantId }
            { name: 'SC_AZURE_SQL_SERVER_NAME', value: sqlServerName }
            { name: 'SC_AZURE_SQL_DATABASE_NAME', value: sqlServerDatabaseName }
            { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsightsInstrumentationKey }
            { name: 'KEYVAULT_URI', value: kvUrl }
            { name: 'KEYVAULT_NAME', value: kvName }
            { name: 'SC_DEPLOY_ENVIRONMENT', value: deployEnvironment }
            { name: 'SC_AZ_LOCATION', value: location }
            { name: 'SC_APP_NAME', value: appName }
            { name: 'SC_AZ_RESSOURCEGROUP_NAME', value: resourceGroup().name }
            { name: 'SC_AZ_WORKER_SUBNET_ID', value: subnetWorkerResourceId }
            { name: 'SC_AZ_DC_SUBNET_ID', value: subnetDcResourceId }
            { name: 'SC_DOMAIN_FQDN', value: domainFQDN }
            { name: 'SC_DOMAIN_CONTROLLER_NAME', value: domainControllerName }
            { name: 'API_APP_CLIENT_ID', value: apiAppClientId }
            { name: 'API_APP_TENANT_ID', value: tenant().tenantId }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

output webAppName string = webApp.name
output webAppFqdn string = webApp.properties.configuration.ingress.fqdn
output apiAppName string = apiApp.name
output apiAppFqdn string = apiApp.properties.configuration.ingress.fqdn
// Kept for backward compatibility with main1.bicep references
output functionAppHostname string = apiApp.properties.configuration.ingress.fqdn
