#Requires -Version 7.0
<# 
.SYNOPSIS
    All-in-one setup and update script for ShieldChecker.

.DESCRIPTION
    Covers the full deployment lifecycle – from first install to rolling updates.
    Phases can be skipped individually with the -Skip* switches when only a subset
    of the stack needs to be refreshed.

    Default run (no -Skip* switches):
      1. App Registrations  – create or update Website + API App Registrations
      2. Resource Group     – create if -CreateRessourceGroupIfNotExistsLocation is set
      3. Bicep              – idempotent infrastructure deployment
      4. SQL                – schema + permissions + initialisation (IF NOT EXISTS guards)
      5. Docker build       – build WebApp (and API if Dockerfile exists) from source
      6. Docker push        – az acr login + tag + push both images to ACR
      7. HostService pkg    – dotnet publish win-x64/linux-x64 + pre-filled appsettings.json + zip
      8. Reply URL update   – set Website App Reg redirect URI to the live Container App FQDN

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
    Container image tag for the web portal image. Default 'latest'.

.PARAMETER ApiImageTag
    Container image tag for the API image. Default 'latest'.

.PARAMETER SkipAppRegistration
    Skip creating / updating the Entra ID App Registrations.
    Useful when only infrastructure or containers need updating.

.PARAMETER SkipBicep
    Skip the Bicep infrastructure deployment.

.PARAMETER SkipSqlSetup
    Skip SQL schema, permissions and initialisation.
    Useful on pure container-image updates.

.PARAMETER SkipDockerBuild
    Skip the 'docker build' step. Assumes local images are already available with
    the names 'shieldchecker-webapp:<tag>' and 'shieldchecker-api:<tag>'.

.PARAMETER SkipDockerPush
    Skip pushing Docker images to ACR.

.PARAMETER SkipHostServicePackage
    Skip building and packaging the HostService distributable zip.

.EXAMPLE
    # Full first-time deploy
    .\Invoke-Deploy.ps1 `
        -SQLAdminGroupName "sg-sql-admin" `
        -ApplicationName "scb" `
        -ResourceGroupName "rg-sc3" `
        -CreateRessourceGroupIfNotExistsLocation "westeurope"

.EXAMPLE
    # Container-image-only update (skip infra + SQL)
    .\Invoke-Deploy.ps1 `
        -SQLAdminGroupName "sg-sql-admin" `
        -ResourceGroupName "rg-sc3" `
        -WebAppImageTag "2.1.0" `
        -SkipAppRegistration -SkipBicep -SkipSqlSetup -SkipHostServicePackage
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

    [string]$ApiImageTag    = 'latest',

    [switch]$SkipAppRegistration,
    [switch]$SkipBicep,
    [switch]$SkipSqlSetup,
    [switch]$SkipDockerBuild,
    [switch]$SkipDockerPush,
    [switch]$SkipHostServicePackage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$BuildName = Get-Date -Format "yyyyMMdd-HHmmss"

# ─────────────────────────────────────────────────────────────────────────────
# Location detection – script works from repo root OR from a Deploy/Latest pkg
# ─────────────────────────────────────────────────────────────────────────────
if (Test-Path (Join-Path $PSScriptRoot "src/Bicep/step1/main1.bicep")) {
    # Running from repo root
    $BicepRoot          = Join-Path $PSScriptRoot "src/Bicep"
    $SqlScriptsRoot     = $null   # generated on-the-fly by dotnet ef (see Phase 4)
    $WebAppDockerfile   = Join-Path $PSScriptRoot "src/Webapp/Dockerfile"
    $WebAppContext      = Join-Path $PSScriptRoot "src/Webapp"
    $ApiDockerfile      = Join-Path $PSScriptRoot "src/Api/Dockerfile"
    $ApiContext         = Join-Path $PSScriptRoot "src/Api"
    $HostServiceSrc     = Join-Path $PSScriptRoot "src/HostService/ShieldChecker.HostService/ShieldChecker.HostService.csproj"
    $PrebuiltSqlDir     = $null
} else {
    # Running from Deploy/Latest (or similar flat package)
    $BicepRoot          = $PSScriptRoot
    $SqlScriptsRoot     = Join-Path $PSScriptRoot "step2/SqlScripts"
    $WebAppDockerfile   = $null   # no source available – use -SkipDockerBuild
    $WebAppContext      = $null
    $ApiDockerfile      = $null
    $ApiContext         = $null
    $HostServiceSrc     = $null
    $PrebuiltSqlDir     = $SqlScriptsRoot
}

$BicepTemplatePath      = Join-Path $BicepRoot "step1/main1.bicep"
$PrebuiltHostWin        = Join-Path $PSScriptRoot "step2/HostService/Windows"
$PrebuiltHostLin        = Join-Path $PSScriptRoot "step2/HostService/Linux"

# ─────────────────────────────────────────────────────────────────────────────
# Helper
# ─────────────────────────────────────────────────────────────────────────────
function Write-Phase([string]$title) {
    Write-Host ""
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
    Write-Host " $title" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 0 – Prerequisites
# ─────────────────────────────────────────────────────────────────────────────
Write-Phase "Phase 0 – Prerequisites"

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

if (-not ($SkipBicep)) {
    Write-Host "Checking Bicep CLI..." -ForegroundColor Green
    try {
        $bicepVersion = bicep --version
        Write-Host " - Bicep $bicepVersion found"
    } catch {
        Write-Host " - Bicep not found. Installing..."
        $installPath = "$env:USERPROFILE\.bicep"
        $installDir  = New-Item -ItemType Directory -Path $installPath -Force
        $installDir.Attributes += 'Hidden'
        (New-Object Net.WebClient).DownloadFile(
            "https://github.com/Azure/bicep/releases/latest/download/bicep-win-x64.exe",
            "$installPath\bicep.exe")
        $currentPath = (Get-Item -path "HKCU:\Environment").GetValue('Path', '', 'DoNotExpandEnvironmentNames')
        if (-not $currentPath.Contains("%USERPROFILE%\.bicep")) { setx PATH ($currentPath + ";%USERPROFILE%\.bicep") }
        if (-not $env:path.Contains($installPath))              { $env:path += ";$installPath" }
        Write-Host " - Bicep installed"
    }
}
    Write-Host "Checking Docker CLI..." -ForegroundColor Green
    try {
        $dockerVersion = docker --version
        Write-Host " - $dockerVersion"
    } catch {
        if (-not $SkipDockerBuild -and -not $SkipDockerPush) {
            throw "Docker CLI is required for image build/push. Install Docker Desktop or set -SkipDockerBuild -SkipDockerPush to skip."
        }
    }
}

if (-not $SkipDockerPush) {
    Write-Host "Checking Azure CLI (for ACR login)..." -ForegroundColor Green
    try {
        $azVersion = az --version 2>&1 | Select-Object -First 1
        Write-Host " - $azVersion"
    } catch {
        throw "Azure CLI ('az') is required to push images to ACR. Install it from https://aka.ms/installazurecli or set -SkipDockerPush to skip."
    }
}

if (-not $SkipHostServicePackage) {
    Write-Host "Checking .NET SDK (for HostService build)..." -ForegroundColor Green
    try {
        $dotnetVersion = dotnet --version
        Write-Host " - .NET SDK $dotnetVersion"
    } catch {
        Write-Warning ".NET SDK not found – HostService will not be built. Set -SkipHostServicePackage to suppress this warning."
        $SkipHostServicePackage = $true
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 1 – App Registrations
# ─────────────────────────────────────────────────────────────────────────────
if ($SkipAppRegistration) {
    Write-Phase "Phase 1 – App Registrations  [SKIPPED]"

    # Still need App IDs for later phases – read from existing registrations
    $WebsiteApp = Get-AzADApplication -DisplayName "baseVISION ShieldChecker ($ApplicationName)"
    $ApiApp     = Get-AzADApplication -DisplayName "baseVISION ShieldChecker API ($ApplicationName)"
    if (-not $WebsiteApp -or -not $ApiApp) {
        throw "App Registrations not found. Run without -SkipAppRegistration at least once."
    }
} else {
    Write-Phase "Phase 1 – App Registrations"

    # Graph token – reuses the current Az session, no second login
    Write-Host "Obtaining Microsoft Graph token..." -ForegroundColor Green
    $graphToken   = (Get-AzAccessToken -ResourceUrl "https://graph.microsoft.com").Token
    $graphHeaders = @{
        Authorization  = "Bearer $graphToken"
        "Content-Type" = "application/json"
    }

    # ── Website App Reg ───────────────────────────────────────────────────────
    Write-Host "Setting up Website App Registration..." -ForegroundColor Green
    $websiteAppName = "baseVISION ShieldChecker ($ApplicationName)"
    $WebsiteApp     = Get-AzADApplication -DisplayName $websiteAppName

    if ($WebsiteApp) {
        Write-Host " - Exists (AppId: $($WebsiteApp.AppId))"
    } else {
        Write-Host " - Creating '$websiteAppName'..."
        $WebsiteApp = New-AzADApplication `
            -DisplayName $websiteAppName `
            -AvailableToOtherTenants $false `
            -Web @{
                ImplicitGrantSetting = @{
                    EnableIdTokenIssuance     = $true
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
        Write-Host " - Created (AppId: $($WebsiteApp.AppId))"
    }

    # ── API App Reg ───────────────────────────────────────────────────────────
    Write-Host "Setting up API App Registration..." -ForegroundColor Green
    $apiAppName = "baseVISION ShieldChecker API ($ApplicationName)"
    $ApiApp     = Get-AzADApplication -DisplayName $apiAppName

    if ($ApiApp) {
        Write-Host " - Exists (AppId: $($ApiApp.AppId))"
    } else {
        Write-Host " - Creating '$apiAppName'..."
        $ApiApp = New-AzADApplication -DisplayName $apiAppName -AvailableToOtherTenants $false
        Write-Host " - Created (AppId: $($ApiApp.AppId))"
    }

    # Idempotent: ensure Application ID URI and 'access_as_host' scope
    Write-Host " - Verifying 'access_as_host' scope on API App..."
    $existingApiDef = Invoke-RestMethod -Method Get `
        -Uri     "https://graph.microsoft.com/v1.0/applications/$($ApiApp.Id)" `
        -Headers $graphHeaders

    $existingScope = $existingApiDef.api.oauth2PermissionScopes |
        Where-Object { $_.value -eq "access_as_host" }

    if ($existingScope) {
        Write-Host " - 'access_as_host' scope already configured"
    } else {
        Write-Host " - Adding 'access_as_host' scope..."
        $newScopeId    = [Guid]::NewGuid().ToString()
        $currentScopes = @($existingApiDef.api.oauth2PermissionScopes | ForEach-Object {
            @{
                id                      = $_.id
                adminConsentDescription = $_.adminConsentDescription
                adminConsentDisplayName = $_.adminConsentDisplayName
                isEnabled               = $false
                type                    = $_.type
                value                   = $_.value
            }
        })
        if ($currentScopes.Count -gt 0) {
            $disableBody = @{ api = @{ oauth2PermissionScopes = $currentScopes } } | ConvertTo-Json -Depth 10
            Invoke-RestMethod -Method Patch `
                -Uri "https://graph.microsoft.com/v1.0/applications/$($ApiApp.Id)" `
                -Headers $graphHeaders -Body $disableBody | Out-Null
        }
        $newScope   = @{
            id                      = $newScopeId
            adminConsentDescription = "Allows the ShieldChecker HostService to interact with the ShieldChecker API."
            adminConsentDisplayName = "Access ShieldChecker API as host"
            isEnabled               = $true
            type                    = "Admin"
            value                   = "access_as_host"
        }
        $allScopes  = ($currentScopes | ForEach-Object { $_.isEnabled = $true; $_ })
        $allScopes  = @($allScopes) + @($newScope)
        $patchBody  = @{
            identifierUris = @("api://$($ApiApp.AppId)")
            api            = @{ oauth2PermissionScopes = $allScopes }
        } | ConvertTo-Json -Depth 10
        Invoke-RestMethod -Method Patch `
            -Uri "https://graph.microsoft.com/v1.0/applications/$($ApiApp.Id)" `
            -Headers $graphHeaders -Body $patchBody | Out-Null
        Write-Host " - Done (scope ID: $newScopeId, URI: api://$($ApiApp.AppId))"
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 2 – Resource Group
# ─────────────────────────────────────────────────────────────────────────────
Write-Phase "Phase 2 – Resource Group"
if (Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue) {
    Write-Host " - Resource group '$ResourceGroupName' found"
} elseif (![String]::IsNullOrWhiteSpace($CreateRessourceGroupIfNotExistsLocation)) {
    Write-Host " - Creating resource group in '$CreateRessourceGroupIfNotExistsLocation'..."
    New-AzResourceGroup -Name $ResourceGroupName -Location $CreateRessourceGroupIfNotExistsLocation
} else {
    throw "Resource group '$ResourceGroupName' not found. Set -CreateRessourceGroupIfNotExistsLocation to create it."
}

# Resolve SQL admin group (always needed for Bicep, even when -SkipBicep, for output references)
Write-Host "Resolving SQL Admin Group '$SQLAdminGroupName'..." -ForegroundColor Green
$SQLAdminGroup = Get-AzADGroup -DisplayName $SQLAdminGroupName
if ($SQLAdminGroup) {
    Write-Host " - Group found (Object ID: $($SQLAdminGroup.Id))"
    $SQLAdminGroupObjectId = $SQLAdminGroup.Id
} else {
    throw "SQL Admin Group '$SQLAdminGroupName' not found. Create the group and add the deploying account as a member."
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 3 – Bicep deployment
# ─────────────────────────────────────────────────────────────────────────────
if ($SkipBicep) {
    Write-Phase "Phase 3 – Bicep deployment  [SKIPPED]"

    # Reconstruct output references from the existing deployment
    Write-Host " - Reading outputs from last successful deployment..."
    $outputMain1 = Get-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName |
        Where-Object { $_.DeploymentName -like "module.network*" -or $_.ProvisioningState -eq "Succeeded" } |
        Sort-Object Timestamp -Descending | Select-Object -First 1

    if (-not $outputMain1) {
        throw "No successful Bicep deployment found in '$ResourceGroupName'. Run without -SkipBicep at least once."
    }

    # Re-run a lightweight deployment to retrieve outputs without changing resources
    $outputMain1 = New-AzResourceGroupDeployment `
        -ResourceGroupName  $ResourceGroupName `
        -TemplateFile       $BicepTemplatePath `
        -applicationDatabaseAdminsGroupName $SQLAdminGroupName `
        -applicationDatabaseAdminsObjectId  $SQLAdminGroupObjectId `
        -deployEnvironment  $DeployEnvironment `
        -domainFQDN         $TestDomainFQDN `
        -appName            $ApplicationName `
        -EnterpriseAppTenantDomain (Get-AzContext).Tenant.Id `
        -EnterpriseAppTenantId     (Get-AzContext).Tenant.Id `
        -EnterpriseAppClientId     $WebsiteApp.AppId `
        -apiAppClientId            $ApiApp.AppId `
        -sleepSeconds       0 `
        -webAppImageTag     $WebAppImageTag `
        -apiImageTag        $ApiImageTag `
        -Mode               Incremental `
        -ErrorAction Stop
} else {
    Write-Phase "Phase 3 – Bicep deployment"

    Write-Host "Deploying Bicep infrastructure (main1.bicep)..." -ForegroundColor Green
    $outputMain1 = New-AzResourceGroupDeployment `
        -ResourceGroupName                   $ResourceGroupName `
        -TemplateFile                        $BicepTemplatePath `
        -applicationDatabaseAdminsGroupName  $SQLAdminGroupName `
        -applicationDatabaseAdminsObjectId   $SQLAdminGroupObjectId `
        -deployEnvironment                   $DeployEnvironment `
        -domainFQDN                          $TestDomainFQDN `
        -appName                             $ApplicationName `
        -EnterpriseAppTenantDomain           $WebsiteApp.PublisherDomain `
        -EnterpriseAppTenantId               (Get-AzContext).Tenant.Id `
        -EnterpriseAppClientId               $WebsiteApp.AppId `
        -apiAppClientId                      $ApiApp.AppId `
        -sleepSeconds                        $SleepSeconds `
        -webAppImageTag                      $WebAppImageTag `
        -apiImageTag                         $ApiImageTag `
        -ErrorAction Stop

    Write-Host " - Bicep deployment completed"
}

# Convenience variables used by multiple phases below
$sqlServerName  = $outputMain1.Outputs["sqlServerName"].Value
$sqlDbName      = $outputMain1.Outputs["sqlServerDatabaseName"].Value
$sqlShortName   = $sqlServerName.Replace(".database.windows.net", "")
$webAppFqdn     = $outputMain1.Outputs["webAppFqdn"].Value
$apiAppFqdn     = $outputMain1.Outputs["apiAppFqdn"].Value
$acrLoginServer = $outputMain1.Outputs["containerRegistryLoginServer"].Value
$acrName        = $outputMain1.Outputs["containerRegistryName"].Value

# ─────────────────────────────────────────────────────────────────────────────
# Phase 4 – SQL setup
# ─────────────────────────────────────────────────────────────────────────────
if ($SkipSqlSetup) {
    Write-Phase "Phase 4 – SQL setup  [SKIPPED]"
} else {
    Write-Phase "Phase 4 – SQL setup"

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

    # Resolve SQL script paths – generate from source when running from repo root
    if ($null -eq $PrebuiltSqlDir) {
        Write-Host " - Generating SQL migration script from source..."
        $sqlTempDir = Join-Path ([System.IO.Path]::GetTempPath()) "sc-sql-$BuildName"
        New-Item -ItemType Directory -Path $sqlTempDir -Force | Out-Null
        $webAppCsproj = Join-Path $PSScriptRoot "src/Webapp/ShieldChecker.WebApp/ShieldChecker.WebApp.csproj"
        dotnet ef migrations script --project $webAppCsproj --idempotent --output (Join-Path $sqlTempDir "sql-database.sql")
        if ($LASTEXITCODE -ne 0) { throw "dotnet ef migrations script failed" }
        $resolvedSqlDir     = $sqlTempDir
        $sqlInitSrc         = Join-Path $PSScriptRoot "src/Webapp/ShieldChecker.WebApp/Models/Db/sql-initialization.sql"
        $sqlPermissionsSrc  = Join-Path $PSScriptRoot "src/Bicep/step2/SqlScripts/sql-permissions.sql"
        Copy-Item $sqlInitSrc        -Destination $sqlTempDir -Force
        Copy-Item $sqlPermissionsSrc -Destination $sqlTempDir -Force
    } else {
        $resolvedSqlDir = $PrebuiltSqlDir
    }

    $permissionsScript = (Get-Content (Join-Path $resolvedSqlDir "sql-permissions.sql") -Raw) `
        -replace "_applicationIdentity_", $outputMain1.Outputs["applicationIdentityName"].Value
    $createScript = Get-Content (Join-Path $resolvedSqlDir "sql-database.sql") -Raw
    $initScript   = (Get-Content (Join-Path $resolvedSqlDir "sql-initialization.sql") -Raw) `
        -replace "_DomainFQDN_", $TestDomainFQDN

    Write-Host " - Applying SQL permissions..."
    Invoke-Sqlcmd -ServerInstance $sqlServerName -Database $sqlDbName -AccessToken $sqlToken -Query $permissionsScript
    Write-Host " - Creating / updating database schema..."
    Invoke-Sqlcmd -ServerInstance $sqlServerName -Database $sqlDbName -AccessToken $sqlToken -Query $createScript
    Write-Host " - Initialising database values..."
    Invoke-Sqlcmd -ServerInstance $sqlServerName -Database $sqlDbName -AccessToken $sqlToken -Query $initScript

    Write-Host " - Removing SQL firewall rule..."
    Remove-AzSqlServerFirewallRule -ResourceGroupName $ResourceGroupName `
        -ServerName $sqlShortName -FirewallRuleName "PublicIPOfDeployment" -Force | Out-Null
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 5 – Docker build
# ─────────────────────────────────────────────────────────────────────────────
if ($SkipDockerBuild) {
    Write-Phase "Phase 5 – Docker build  [SKIPPED]"
    Write-Host " - Assumes local images are already tagged:"
    Write-Host "     shieldchecker-webapp:$WebAppImageTag"
    Write-Host "     shieldchecker-api:$ApiImageTag"
} else {
    Write-Phase "Phase 5 – Docker build"

    # WebApp
    if ($null -ne $WebAppDockerfile -and (Test-Path $WebAppDockerfile)) {
        Write-Host " - Building shieldchecker-webapp:$WebAppImageTag ..."
        docker build -t "shieldchecker-webapp:$WebAppImageTag" -f $WebAppDockerfile $WebAppContext
        if ($LASTEXITCODE -ne 0) { throw "docker build failed for webapp (exit code $LASTEXITCODE)" }
        Write-Host " - WebApp image built successfully"
    } else {
        Write-Warning " - WebApp Dockerfile not found. Skipping WebApp build (use -SkipDockerBuild to suppress)."
    }

    # API
    if ($null -ne $ApiDockerfile -and (Test-Path $ApiDockerfile)) {
        Write-Host " - Building shieldchecker-api:$ApiImageTag ..."
        docker build -t "shieldchecker-api:$ApiImageTag" -f $ApiDockerfile $ApiContext
        if ($LASTEXITCODE -ne 0) { throw "docker build failed for api (exit code $LASTEXITCODE)" }
        Write-Host " - API image built successfully"
    } else {
        Write-Warning " - API Dockerfile not found at '$ApiDockerfile'. Skipping API build."
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 6 – Docker push to ACR
# ─────────────────────────────────────────────────────────────────────────────
if ($SkipDockerPush) {
    Write-Phase "Phase 6 – Docker push  [SKIPPED]"
} else {
    Write-Phase "Phase 6 – Docker push to ACR"

    Write-Host " - Logging in to ACR '$acrName' ($acrLoginServer)..."
    az acr login --name $acrName
    if ($LASTEXITCODE -ne 0) { throw "az acr login failed (exit code $LASTEXITCODE)" }

    foreach ($entry in @(
        @{ Local = "shieldchecker-webapp:$WebAppImageTag"; Remote = "${acrLoginServer}/shieldchecker-webapp:$WebAppImageTag" },
        @{ Local = "shieldchecker-api:$ApiImageTag";       Remote = "${acrLoginServer}/shieldchecker-api:$ApiImageTag" }
    )) {
        # Check the local image actually exists before trying to push
        $imageExists = docker image inspect $entry.Local 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Warning " - Local image '$($entry.Local)' not found – skipping push."
            continue
        }

        Write-Host " - Tagging  : $($entry.Local) → $($entry.Remote)"
        docker tag $entry.Local $entry.Remote
        if ($LASTEXITCODE -ne 0) { throw "docker tag failed for '$($entry.Local)'" }

        Write-Host " - Pushing  : $($entry.Remote)"
        docker push $entry.Remote
        if ($LASTEXITCODE -ne 0) { throw "docker push failed for '$($entry.Remote)'" }
    }

    Write-Host " - All available images pushed to ACR"
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 7 – HostService package
# ─────────────────────────────────────────────────────────────────────────────
if ($SkipHostServicePackage) {
    Write-Phase "Phase 7 – HostService package  [SKIPPED]"
} else {
    Write-Phase "Phase 7 – HostService package"

    $hostSvcPkgDir = Join-Path $PSScriptRoot "HostServicePackage"
    $winOutDir     = Join-Path $hostSvcPkgDir "Windows"
    $linOutDir     = Join-Path $hostSvcPkgDir "Linux"
    New-Item -ItemType Directory -Path $winOutDir -Force | Out-Null
    New-Item -ItemType Directory -Path $linOutDir -Force | Out-Null

    # ── Build binaries ────────────────────────────────────────────────────────
    if ($null -ne $HostServiceSrc -and (Test-Path $HostServiceSrc)) {
        Write-Host " - Building HostService (win-x64)..."
        dotnet publish $HostServiceSrc --self-contained --configuration Release --runtime win-x64   -o $winOutDir
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish (win-x64) failed" }
        Write-Host " - Building HostService (linux-x64)..."
        dotnet publish $HostServiceSrc --self-contained --configuration Release --runtime linux-x64 -o $linOutDir
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish (linux-x64) failed" }
    } else {
        # Fall back to pre-built binaries (Deploy/Latest layout)
        if (Test-Path $PrebuiltHostWin) {
            Write-Host " - Copying pre-built Windows HostService binaries..."
            Copy-Item -Path "$PrebuiltHostWin/*" -Destination $winOutDir -Recurse -Force
        } else {
            Write-Warning " - No HostService source or pre-built Windows binaries found. Windows package will be empty."
        }
        if (Test-Path $PrebuiltHostLin) {
            Write-Host " - Copying pre-built Linux HostService binaries..."
            Copy-Item -Path "$PrebuiltHostLin/*" -Destination $linOutDir -Recurse -Force
        } else {
            Write-Warning " - No HostService source or pre-built Linux binaries found. Linux package will be empty."
        }
    }

    # ── Generate pre-filled appsettings.json ─────────────────────────────────
    Write-Host " - Generating pre-filled appsettings.json..."
    $hostSvcConfig = [ordered]@{
        Logging      = [ordered]@{
            LogLevel = [ordered]@{
                Default                        = "Information"
                "Microsoft.Hosting.Lifetime"   = "Information"
            }
        }
        ShieldCheckerApi = [ordered]@{
            Hostname = $apiAppFqdn
            Scope    = "api://$($ApiApp.AppId)/access_as_host"
        }
        AzureAd = [ordered]@{
            TenantId     = (Get-AzContext).Tenant.Id
            ClientId     = "<App Registration Client ID for this agent>"
            ClientSecret = "<App Registration Client Secret for this agent>"
        }
    }
    $configJson = $hostSvcConfig | ConvertTo-Json -Depth 10
    Set-Content -Path (Join-Path $winOutDir "appsettings.json") -Value $configJson -Force
    Set-Content -Path (Join-Path $linOutDir "appsettings.json") -Value $configJson -Force

    # ── Zip the package ───────────────────────────────────────────────────────
    $zipPath = Join-Path $PSScriptRoot "ShieldChecker-HostService.zip"
    Compress-Archive -Path "$hostSvcPkgDir/*" -DestinationPath $zipPath -Force
    Remove-Item -Path $hostSvcPkgDir -Recurse -Force

    Write-Host " - HostService package ready: $zipPath"
    Write-Host " - Distribute this zip to each agent machine, then run:" -ForegroundColor Yellow
    Write-Host "     Windows : .\ScHostSvc.exe --setup" -ForegroundColor Cyan
    Write-Host "     Linux   : ./ScHostSvc --setup" -ForegroundColor Cyan
    Write-Host " - The pre-filled appsettings.json already contains:" -ForegroundColor Yellow
    Write-Host "     API Hostname : $apiAppFqdn" -ForegroundColor Cyan
    Write-Host "     API Scope    : api://$($ApiApp.AppId)/access_as_host" -ForegroundColor Cyan
    Write-Host "     Tenant ID    : $((Get-AzContext).Tenant.Id)" -ForegroundColor Cyan
    Write-Host " - Each agent still needs its OWN App Registration client ID + secret." -ForegroundColor Yellow
}

# ─────────────────────────────────────────────────────────────────────────────
# Phase 8 – Update Website App Registration reply URL
# ─────────────────────────────────────────────────────────────────────────────
Write-Phase "Phase 8 – Update Website App Registration reply URL"
Update-AzADApplication `
    -ApplicationId $WebsiteApp.AppId `
    -ReplyUrl @("https://$webAppFqdn/signin-oidc")
Write-Host " - Reply URL set to: https://$webAppFqdn/signin-oidc"

# ─────────────────────────────────────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────────────────────────────────────
Write-Phase "Deployment complete"
Write-Host ""
Write-Host "  Web portal : https://$webAppFqdn" -ForegroundColor Green
Write-Host "  API        : https://$apiAppFqdn" -ForegroundColor Green
Write-Host ""
Write-Host "  Infrastructure details (keep for future updates):" -ForegroundColor Green
Write-Host "    Resource Group           : $ResourceGroupName"
Write-Host "    Application Name         : $ApplicationName"
Write-Host "    SQL Server               : $sqlServerName"
Write-Host "    SQL Database             : $sqlDbName"
Write-Host "    ACR                      : $acrLoginServer"
Write-Host "    Domain FQDN              : $TestDomainFQDN"
Write-Host "    Website App Registration : $($WebsiteApp.AppId)"
Write-Host "    API App Registration     : $($ApiApp.AppId)"

 

