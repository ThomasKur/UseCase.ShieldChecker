@allowed([
  'dev'
  'prd'
])
@description('Deployment environment.')
param deployEnvironment string = 'dev'

@description('Location for all resources.')
param location string 

param appName string

param applicationIdentityPrincipalId string

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${substring(appName,0,min(length(appName), 8))}-${substring(uniqueString(subscription().subscriptionId),0,4)}-${deployEnvironment}-001'
  location: location
  properties: {
    enabledForDeployment: true
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableRbacAuthorization: true
    tenantId: tenant().tenantId
    enableSoftDelete: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    sku: {
      name: 'standard'
      family: 'A'
    }
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

@description('This is the built-in Key Vault Secret User role. See https://docs.microsoft.com/azure/role-based-access-control/built-in-roles#key-vault-secrets-user')
resource keyVaultSecretUserRoleRoleDefinition 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

@description('Grant the app service identity with key vault secret user role permissions over the key vault. This allows reading secret contents')
resource keyVaultSecretUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
  scope: kv
  name: guid(resourceGroup().id, keyVaultSecretUserRoleRoleDefinition.id)
  properties: {
    roleDefinitionId: keyVaultSecretUserRoleRoleDefinition.id
    principalId: applicationIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output kvUrl string = kv.properties.vaultUri
output kvName string = kv.name
