#Requires -Version 7.0
<# 
.SYNOPSIS
    Unified deployment script for ShieldChecker.

.DESCRIPTION
    Sets up App Registrations for the web portal and API, deploys the Bicep
    infrastructure, configures the SQL database, and reports container image
    push instructions for the Azure Container Registry.

.PARAMETER ApplicationName
    Short lowercase identifier for the deployment. Default is 'shieldchecker'.

.PARAMETER SQLAdminGroupName
    Entra ID group that will be the SQL server administrator. Mandatory.

.PARAMETER DeployEnvironment
    Target environment ('dev' or 'prd'). Default is 'prd'.

.PARAMETER ResourceGroupName
    Azure resource group for all resources. Mandatory.

.PARAMETER TestDomainFQDN
    FQDN of the ShieldChecker test domain. Default is 'shieldchecker.local'.

.PARAMETER CreateRessourceGroupIfNotExistsLocation
    When set, creates the resource group in this Azure region if it does not exist.

.PARAMETER SleepSeconds
    Seconds to wait between operations that need stabilisation. Default 60.

.PARAMETER WebAppImageTag
    Container image tag for the web portal image in ACR. Default 'latest'.

.PARAMETER ApiImageTag
    Container image tag for the API image in ACR. Default 'latest'.

.EXAMPLE
    .\Invoke-Deploy.ps1 `
        -SQLAdminGroupName "sg-sql-admin" `
        -ApplicationName "scb" `
        -TestDomainFQDN "shieldchecker.local" `
        -ResourceGroupName "rg-sc3" `
        -CreateRessourceGroupIfNotExistsLocation "westeurope"
#>
param(
    [ValidatePattern('^[a-z]+$')] 
    [string]$ApplicationName = 'shieldchecker',

    [parameter(Mandatory=$true)]
    [string]$SQLAdminGroupName,

    [ValidateSet('dev','prd')]
    [string]$DeployEnvironment = 'prd',

    [parameter(Mandatory=$true)]
    [string]$ResourceGroupName,

    [string]$TestDomainFQDN = 'shieldchecker.local',

    [Alias("CreateRGLocation")]
    [string]$CreateRessourceGroupIfNotExistsLocation,

    [ValidateRange(1, 120)]
    [int]$SleepSeconds = 60,

    [string]$WebAppImageTag = 'latest',

    [string]$ApiImageTag = 'latest'
)

# ── Prerequisites ────────────────────────────────────────────────────────────

Write-Host "Checking Azure context..." -ForegroundColor Green
if (Get-AzContext -ErrorAction SilentlyContinue) {
    Write-Host " - Azure context found: $((Get-AzContext).Account.Id)"
} else {
    throw "Azure context not found. Please run 'Connect-AzAccount' and select the correct subscription."
}

Write-Host "Checking required PowerShell modules..." -ForegroundColor Green
$reqModules = @("Az.Accounts", "Az.Resources", "Az.Sql", "SQLServer")
$reqModules | ForEach-Object {
    if (-not (Get-Module -ListAvailable -Name $_)) {
        Write-Host "  - Installing module $_"
        Install-Module -Name $_ -Force -Scope CurrentUser -AllowClobber -ErrorAction Stop
    } else {
        Write-Host "  - Module $_ is available"
        Import-Module -Name $_ -ErrorAction SilentlyContinue
    }
}

Write-Host "Checking bicep CLI..." -ForegroundColor Green
try {
    $bicepVersion = bicep --version
    Write-Host " - Bicep $bicepVersion found"
} catch {
    Write-Host " - Bicep not found. Installing..."
    $installPath = "$env:USERPROFILE\.bicep"
    $installDir = New-Item -ItemType Directory -Path $installPath -Force
    $installDir.Attributes += 'Hidden'
    (New-Object Net.WebClient).DownloadFile(
        "https://github.com/Azure/bicep/releases/latest/download/bicep-win-x64.exe",
        "$installPath\bicep.exe")
    $currentPath = (Get-Item -path "HKCU:\Environment").GetValue('Path', '', 'DoNotExpandEnvironmentNames')
    if (-not $currentPath.Contains("%USERPROFILE%\.bicep")) {
        setx PATH ($currentPath + ";%USERPROFILE%\.bicep")
    }
    if (-not $env:path.Contains($installPath)) { $env:path += ";$installPath" }
    Write-Host " - Bicep installed"
}

# ── SQL Admin Group ──────────────────────────────────────────────────────────

Write-Host "Resolving SQL Admin Group '$SQLAdminGroupName'..." -ForegroundColor Green
$SQLAdminGroup = Get-AzADGroup -DisplayName $SQLAdminGroupName
if ($SQLAdminGroup) {
    Write-Host " - Group found (Object ID: $($SQLAdminGroup.Id))"
    $SQLAdminGroupObjectId = $SQLAdminGroup.Id
} else {
    throw "SQL Admin Group '$SQLAdminGroupName' not found. Create the group and add the deploying account as a member."
}

# ── Microsoft Graph token (reuses existing Az session – no double login) ────

Write-Host "Obtaining Microsoft Graph token from current Az session..." -ForegroundColor Green
$graphToken    = (Get-AzAccessToken -ResourceUrl "https://graph.microsoft.com").Token
$graphHeaders  = @{
    Authorization  = "Bearer $graphToken"
    "Content-Type" = "application/json"
}

# ── Website App Registration ─────────────────────────────────────────────────

Write-Host "Setting up Website App Registration..." -ForegroundColor Green
$websiteAppName = "baseVISION ShieldChecker ($ApplicationName)"
$WebsiteApp     = Get-AzADApplication -DisplayName $websiteAppName

if ($WebsiteApp) {
    Write-Host " - App '$websiteAppName' already exists (AppId: $($WebsiteApp.AppId))"
} else {
    Write-Host " - Creating App '$websiteAppName'..."
    $WebsiteApp = New-AzADApplication `
        -DisplayName $websiteAppName `
        -AvailableToOtherTenants $false `
        -Web @{
            ImplicitGrantSetting = @{
                EnableIdTokenIssuance    = $true
                EnableAccessTokenIssuance = $false
            }
            RedirectUri = @("https://localhost/signin-oidc")
        } `
        -RequiredResourceAccess @(
            @{
                ResourceAppId  = "00000003-0000-0000-c000-000000000000"
                ResourceAccess = @(
                    @{ Id = "7ab1d382-f21e-4acd-a863-ba3e13f7da61"; Type = "Scope" },
                    @{ Id = "14dad69e-099b-42c9-810b-d002981feec1"; Type = "Scope" }
                )
            }
        )
    Write-Host " - App '$websiteAppName' created (AppId: $($WebsiteApp.AppId))"
}

# ── API App Registration ─────────────────────────────────────────────────────

Write-Host "Setting up API App Registration..." -ForegroundColor Green
$apiAppName = "baseVISION ShieldChecker API ($ApplicationName)"
$ApiApp     = Get-AzADApplication -DisplayName $apiAppName

if ($ApiApp) {
    Write-Host " - App '$apiAppName' already exists (AppId: $($ApiApp.AppId))"
} else {
    Write-Host " - Creating App '$apiAppName'..."
    $ApiApp = New-AzADApplication -DisplayName $apiAppName -AvailableToOtherTenants $false
    Write-Host " - App '$apiAppName' created (AppId: $($ApiApp.AppId))"
}

# Ensure Application ID URI and 'access_as_host' scope are set (idempotent)
Write-Host " - Verifying 'access_as_host' scope on API App..."
$existingApiDef = Invoke-RestMethod `
    -Method  Get `
    -Uri     "https://graph.microsoft.com/v1.0/applications/$($ApiApp.Id)" `
    -Headers $graphHeaders

$existingScope = $existingApiDef.api.oauth2PermissionScopes |
    Where-Object { $_.value -eq "access_as_host" }

if ($existingScope) {
    Write-Host " - 'access_as_host' scope already configured"
} else {
    Write-Host " - Adding 'access_as_host' scope..."
    $newScopeId = [Guid]::NewGuid().ToString()

    # Preserve any existing scopes (disable them first per Graph API requirement)
    $currentScopes = @($existingApiDef.api.oauth2PermissionScopes | ForEach-Object {
        @{
            id                      = $_.id
            adminConsentDescription = $_.adminConsentDescription
            adminConsentDisplayName = $_.adminConsentDisplayName
            isEnabled               = $false   # must disable before adding new ones
            type                    = $_.type
            value                   = $_.value
        }
    })

    $disableBody = @{ api = @{ oauth2PermissionScopes = $currentScopes } } | ConvertTo-Json -Depth 10
    if ($currentScopes.Count -gt 0) {
        Invoke-RestMethod -Method Patch `
            -Uri "https://graph.microsoft.com/v1.0/applications/$($ApiApp.Id)" `
            -Headers $graphHeaders -Body $disableBody | Out-Null
    }

    $newScope = @{
        id                      = $newScopeId
        adminConsentDescription = "Allows the ShieldChecker HostService to interact with the ShieldChecker API."
        adminConsentDisplayName = "Access ShieldChecker API as host"
        isEnabled               = $true
        type                    = "Admin"
        value                   = "access_as_host"
    }
    $allScopes = $currentScopes | ForEach-Object { $_.isEnabled = $true; $_ }
    $allScopes  = @($allScopes) + @($newScope)

    $patchBody = @{
        identifierUris = @("api://$($ApiApp.AppId)")
        api            = @{ oauth2PermissionScopes = $allScopes }
    } | ConvertTo-Json -Depth 10

    Invoke-RestMethod -Method Patch `
        -Uri "https://graph.microsoft.com/v1.0/applications/$($ApiApp.Id)" `
        -Headers $graphHeaders -Body $patchBody | Out-Null

    Write-Host " - 'access_as_host' scope added (ID: $newScopeId)"
    Write-Host " - Application ID URI set to: api://$($ApiApp.AppId)"
}

# ── Resource Group ────────────────────────────────────────────────────────────

Write-Host "Checking Resource Group '$ResourceGroupName'..." -ForegroundColor Green
if (Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue) {
    Write-Host " - Resource group found"
} else {
    if (![String]::IsNullOrWhiteSpace($CreateRessourceGroupIfNotExistsLocation)) {
        Write-Host " - Creating resource group in '$CreateRessourceGroupIfNotExistsLocation'..."
        New-AzResourceGroup -Name $ResourceGroupName -Location $CreateRessourceGroupIfNotExistsLocation
    } else {
        throw "Resource group '$ResourceGroupName' not found. Set -CreateRessourceGroupIfNotExistsLocation to create it."
    }
}

# ── Bicep Deployment ─────────────────────────────────────────────────────────

Write-Host "Deploying Bicep infrastructure (main1.bicep)..." -ForegroundColor Green
$outputMain1 = New-AzResourceGroupDeployment `
    -ResourceGroupName                    $ResourceGroupName `
    -TemplateFile                         "$PSScriptRoot/step1/main1.bicep" `
    -applicationDatabaseAdminsGroupName   $SQLAdminGroupName `
    -applicationDatabaseAdminsObjectId    $SQLAdminGroupObjectId `
    -deployEnvironment                    $DeployEnvironment `
    -domainFQDN                           $TestDomainFQDN `
    -appName                              $ApplicationName `
    -EnterpriseAppTenantDomain            $WebsiteApp.PublisherDomain `
    -EnterpriseAppTenantId                (Get-AzContext).Tenant.Id `
    -EnterpriseAppClientId                $WebsiteApp.AppId `
    -apiAppClientId                       $ApiApp.AppId `
    -sleepSeconds                         $SleepSeconds `
    -webAppImageTag                       $WebAppImageTag `
    -apiImageTag                          $ApiImageTag `
    -ErrorAction Stop

Write-Host " - Bicep deployment completed"

# ── SQL Database Setup ───────────────────────────────────────────────────────

Write-Host "Configuring SQL database..." -ForegroundColor Green
$sqlServerName = $outputMain1.Outputs["sqlServerName"].Value
$sqlDbName     = $outputMain1.Outputs["sqlServerDatabaseName"].Value
$sqlShortName  = $sqlServerName.Replace(".database.windows.net", "")

$publicIp = (Invoke-RestMethod -Uri "http://api.ipify.org")
Write-Host " - Adding SQL firewall rule for $publicIp"
New-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName `
    -ServerName $sqlShortName `
    -FirewallRuleName "PublicIPOfDeployment" `
    -StartIpAddress $publicIp -EndIpAddress $publicIp | Out-Null

Write-Host " - Waiting $SleepSeconds seconds for firewall rule to propagate..."
Start-Sleep -Seconds $SleepSeconds

Write-Host " - Obtaining SQL access token..."
$env:SuppressAzurePowerShellBreakingChangeWarnings = $true
$sqlTokenSecure = (Get-AzAccessToken -ResourceUrl https://database.windows.net -AsSecureString).Token
$sqlToken = [System.Net.NetworkCredential]::new("", $sqlTokenSecure).Password
$env:SuppressAzurePowerShellBreakingChangeWarnings = $false

$permissionsScript = Get-Content "$PSScriptRoot/step2/SqlScripts/sql-permissions.sql" -Raw
$permissionsScript = $permissionsScript -replace "_applicationIdentity_", $outputMain1.Outputs["applicationIdentityName"].Value

$createScript     = Get-Content "$PSScriptRoot/step2/SqlScripts/sql-database.sql" -Raw
$initScript       = Get-Content "$PSScriptRoot/step2/SqlScripts/sql-initialization.sql" -Raw
$initScript       = $initScript -replace "_DomainFQDN_", $TestDomainFQDN

Write-Host " - Applying SQL permissions..."
Invoke-Sqlcmd -ServerInstance $sqlServerName -Database $sqlDbName -AccessToken $sqlToken -Query $permissionsScript
Write-Host " - Creating database schema..."
Invoke-Sqlcmd -ServerInstance $sqlServerName -Database $sqlDbName -AccessToken $sqlToken -Query $createScript
Write-Host " - Initialising database values..."
Invoke-Sqlcmd -ServerInstance $sqlServerName -Database $sqlDbName -AccessToken $sqlToken -Query $initScript

Write-Host " - Removing SQL firewall rule..."
Remove-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName `
    -ServerName $sqlShortName -FirewallRuleName "PublicIPOfDeployment" -Force | Out-Null

# ── Update Website App Registration Reply URL ────────────────────────────────

Write-Host "Updating Website App Registration reply URL..." -ForegroundColor Green
$webAppFqdn = $outputMain1.Outputs["webAppFqdn"].Value
Update-AzADApplication `
    -ApplicationId $WebsiteApp.AppId `
    -ReplyUrl @("https://$webAppFqdn/signin-oidc")
Write-Host " - Reply URL set to: https://$webAppFqdn/signin-oidc"

# ── Container Images ─────────────────────────────────────────────────────────

$acrLoginServer = $outputMain1.Outputs["containerRegistryLoginServer"].Value
$acrName        = $outputMain1.Outputs["containerRegistryName"].Value

Write-Host ""
Write-Host "Container images must be pushed to the Azure Container Registry." -ForegroundColor Yellow
Write-Host "ACR login server : $acrLoginServer" -ForegroundColor Yellow
Write-Host "ACR name         : $acrName" -ForegroundColor Yellow
Write-Host ""
Write-Host "Push commands (run after building Docker images with Invoke-Build.ps1):" -ForegroundColor Yellow
Write-Host "  az acr login --name $acrName" -ForegroundColor Cyan
Write-Host "  docker tag shieldchecker-webapp:$WebAppImageTag ${acrLoginServer}/shieldchecker-webapp:$WebAppImageTag" -ForegroundColor Cyan
Write-Host "  docker push ${acrLoginServer}/shieldchecker-webapp:$WebAppImageTag" -ForegroundColor Cyan
Write-Host "  docker tag shieldchecker-api:$ApiImageTag ${acrLoginServer}/shieldchecker-api:$ApiImageTag" -ForegroundColor Cyan
Write-Host "  docker push ${acrLoginServer}/shieldchecker-api:$ApiImageTag" -ForegroundColor Cyan

# ── Summary ──────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Deployment completed successfully." -ForegroundColor Green
Write-Host ""
Write-Host "Web portal  : https://$webAppFqdn"
Write-Host "API         : https://$($outputMain1.Outputs["apiAppFqdn"].Value)"
Write-Host ""
Write-Host "HostService setup values (needed during 'ScHostSvc --setup' on each agent):" -ForegroundColor Green
Write-Host "  ShieldChecker API hostname : $($outputMain1.Outputs["apiAppFqdn"].Value)"
Write-Host "  ShieldChecker API scope    : api://$($ApiApp.AppId)/.default"
Write-Host ""
Write-Host "Infrastructure details (keep for future updates):" -ForegroundColor Green
Write-Host "  Resource Group             : $ResourceGroupName"
Write-Host "  Application Name           : $ApplicationName"
Write-Host "  SQL Server                 : $sqlServerName"
Write-Host "  SQL Database               : $sqlDbName"
Write-Host "  SQL Admin Group            : $SQLAdminGroupName"
Write-Host "  Domain FQDN                : $TestDomainFQDN"
Write-Host "  Website App Registration   : $($WebsiteApp.AppId)"
Write-Host "  API App Registration       : $($ApiApp.AppId)  (Application ID URI: api://$($ApiApp.AppId))"
 

