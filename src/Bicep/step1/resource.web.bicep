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

@description('Client ID of the ShieldChecker-BackendApi app registration (WebApp.Access AppRole).')
param backendApiAppClientId string

@description('Client ID of the ShieldChecker-HostServiceApi app registration (HostService.Access AppRole).')
param hostServiceApiAppClientId string

@description('Client ID of the ShieldChecker-ImportApi app registration (Import.SharedLibrary AppRole).')
param importApiAppClientId string

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

@description('Image tag for the BackendApi container.')
param backendApiImageTag string = 'latest'

@description('Image tag for the HostServiceApi container.')
param hostServiceApiImageTag string = 'latest'

@description('Image tag for the ImportApi container.')
param importApiImageTag string = 'latest'

var sqlConnectionString = 'Server=tcp:${sqlServerName},1433;Initial Catalog=${sqlServerDatabaseName};Authentication=Active Directory Default;Encrypt=True;MultipleActiveResultSets=True;'

// ── WebApp Container App ──────────────────────────────────────────────────────
// External ingress – served to browser users authenticated via OIDC.
// No direct SQL access; all data goes through the BackendApi over the internal network.

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
            // Internal URL of the BackendApi – only reachable inside the Container Apps environment
            { name: 'BACKEND_API_BASE_URL', value: 'https://${backendApiApp.properties.configuration.ingress.fqdn}' }
            { name: 'BACKEND_API_CLIENT_ID', value: backendApiAppClientId }
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

// ── BackendApi Container App ──────────────────────────────────────────────────
// INTERNAL ingress – reachable only from within the Container Apps Environment.
// Serves all data-access operations for the WebApp; validates WebApp.Access AppRole.

resource backendApiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${appName}-backendapi-${deployEnvironment}-001'
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
        external: false   // Internal only – not reachable from the internet
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
          name: 'backendapi'
          image: '${containerRegistryLoginServer}/shieldchecker-backendapi:${backendApiImageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'AZURE_CLIENT_ID', value: applicationIdentityClientId }
            { name: 'AZURE_TENANT_ID', value: tenant().tenantId }
            { name: 'AzureSqlDatabase', value: sqlConnectionString }
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
            // JWT validation – ShieldChecker-BackendApi app registration
            { name: 'AzureAd__Instance', value: environment().authentication.loginEndpoint }
            { name: 'AzureAd__TenantId', value: tenant().tenantId }
            { name: 'AzureAd__ClientId', value: backendApiAppClientId }
            { name: 'AzureAd__Audience', value: 'api://${backendApiAppClientId}' }
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

// ── HostServiceApi Container App ──────────────────────────────────────────────
// EXTERNAL ingress – reachable by customer HostService agents over the internet.
// Serves GET /api/Job and POST /api/JobUpdater; validates HostService.Access AppRole.

resource hostServiceApiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${appName}-hostserviceapi-${deployEnvironment}-001'
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
        external: true   // Accessible by customer HostService agents over the internet
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
          name: 'hostserviceapi'
          image: '${containerRegistryLoginServer}/shieldchecker-hostserviceapi:${hostServiceApiImageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'AZURE_CLIENT_ID', value: applicationIdentityClientId }
            { name: 'AZURE_TENANT_ID', value: tenant().tenantId }
            { name: 'AzureSqlDatabase', value: sqlConnectionString }
            { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsightsInstrumentationKey }
            { name: 'KEYVAULT_URI', value: kvUrl }
            // JWT validation – ShieldChecker-HostServiceApi app registration
            { name: 'AzureAd__Instance', value: environment().authentication.loginEndpoint }
            { name: 'AzureAd__TenantId', value: tenant().tenantId }
            { name: 'AzureAd__ClientId', value: hostServiceApiAppClientId }
            { name: 'AzureAd__Audience', value: 'api://${hostServiceApiAppClientId}' }
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

// ── ImportApi Container App ────────────────────────────────────────────────────
// EXTERNAL ingress – reachable by the operator import script.
// Restrict access to known operator IP ranges via IP-allow rules in production.
// Validates Import.SharedLibrary AppRole.

resource importApiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${appName}-importapi-${deployEnvironment}-001'
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
        external: true   // Accessible by the operator import script
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
          name: 'importapi'
          image: '${containerRegistryLoginServer}/shieldchecker-importapi:${importApiImageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'AZURE_CLIENT_ID', value: applicationIdentityClientId }
            { name: 'AZURE_TENANT_ID', value: tenant().tenantId }
            { name: 'AzureSqlDatabase', value: sqlConnectionString }
            { name: 'APPINSIGHTS_INSTRUMENTATIONKEY', value: appInsightsInstrumentationKey }
            { name: 'KEYVAULT_URI', value: kvUrl }
            // JWT validation – ShieldChecker-ImportApi app registration
            { name: 'AzureAd__Instance', value: environment().authentication.loginEndpoint }
            { name: 'AzureAd__TenantId', value: tenant().tenantId }
            { name: 'AzureAd__ClientId', value: importApiAppClientId }
            { name: 'AzureAd__Audience', value: 'api://${importApiAppClientId}' }
          ]
        }
      ]
      scale: {
        minReplicas: 0   // Scale to zero when not in use to save cost
        maxReplicas: 1
      }
    }
  }
}

output webAppName string = webApp.name
output webAppFqdn string = webApp.properties.configuration.ingress.fqdn
output backendApiAppName string = backendApiApp.name
output backendApiAppFqdn string = backendApiApp.properties.configuration.ingress.fqdn
output hostServiceApiAppName string = hostServiceApiApp.name
output hostServiceApiAppFqdn string = hostServiceApiApp.properties.configuration.ingress.fqdn
output importApiAppName string = importApiApp.name
output importApiAppFqdn string = importApiApp.properties.configuration.ingress.fqdn
// Kept for backward compatibility
output apiAppName string = backendApiApp.name
output apiAppFqdn string = backendApiApp.properties.configuration.ingress.fqdn
output functionAppHostname string = backendApiApp.properties.configuration.ingress.fqdn

