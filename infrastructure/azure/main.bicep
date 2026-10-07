targetScope = 'resourceGroup'

@description('Azure region for the app and Key Vault.')
param location string = resourceGroup().location

@description('Globally unique lowercase App Service name.')
param appName string

@description('Container image reference published by the release process.')
param containerImage string

@description('Globally unique Key Vault name.')
param keyVaultName string

@description('Public HTTPS base URL used for payment callbacks.')
param publicBaseUrl string = 'https://${appName}.azurewebsites.net'

@description('Start in simulated mode unless the operator explicitly changes it after configuring Key Vault secrets.')
param demoMode bool = true

@description('Payment provider endpoint environment.')
@allowed([
  'sandbox'
  'live'
])
param providerEnvironment string = 'sandbox'

@description('Bootstrap administrator email. The password is held in Key Vault.')
param adminEmail string = ''

@description('The default KargoJet carrier code.')
param kargoJetCarrierCode string = 'aras'

@description('Flat shipping fee in Turkish lira.')
param flatShippingFee string = '49'

@description('Free shipping threshold in Turkish lira.')
param freeShippingThreshold string = '1500'

var keyVaultRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource appPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
    size: 'B1'
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource secretsVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
    accessPolicies: []
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'true' }
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
        { name: 'birmarket_demo_mode', value: string(demoMode) }
        { name: 'birmarket_demo_sqlite_path', value: '/home/data/birmarket-demo.sqlite3' }
        { name: 'birmarket_real_sqlite_path', value: '/home/data/birmarket.sqlite3' }
        { name: 'birmarket_provider_environment', value: providerEnvironment }
        { name: 'birmarket_public_base_url', value: publicBaseUrl }
        { name: 'birmarket_admin_email', value: adminEmail }
        { name: 'birmarket_admin_password', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=birmarket-admin-password)' }
        { name: 'birmarket_data_protection_key', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=birmarket-data-protection-key)' }
        { name: 'iyzico_api_key', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=iyzico-api-key)' }
        { name: 'iyzico_secret_key', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=iyzico-secret-key)' }
        { name: 'paytr_merchant_id', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=paytr-merchant-id)' }
        { name: 'paytr_merchant_key', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=paytr-merchant-key)' }
        { name: 'paytr_merchant_salt', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=paytr-merchant-salt)' }
        { name: 'kargojet_api_token', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=kargojet-api-token)' }
        { name: 'kargojet_webhook_secret', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=kargojet-webhook-secret)' }
        { name: 'kargojet_carrier_code', value: kargoJetCarrierCode }
        { name: 'smtp_host', value: 'smtp.azurecomm.net' }
        { name: 'smtp_port', value: '587' }
        { name: 'smtp_username', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=smtp-username)' }
        { name: 'smtp_password', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=smtp-password)' }
        { name: 'email_from', value: demoMode ? '' : '@Microsoft.KeyVault(VaultName=${secretsVault.name};SecretName=email-from)' }
        { name: 'email_from_name', value: 'Birmarket' }
        { name: 'birmarket_flat_shipping_fee', value: flatShippingFee }
        { name: 'birmarket_free_shipping_threshold', value: freeShippingThreshold }
      ]
    }
  }
}

resource secretsAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(secretsVault.id, webApp.id, keyVaultRoleId)
  scope: secretsVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultRoleId)
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output siteHost string = webApp.properties.defaultHostName
output keyVaultResourceId string = secretsVault.id
