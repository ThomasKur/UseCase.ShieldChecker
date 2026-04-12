<#
.SYNOPSIS
    Imports Atomic Red Team tests into the ShieldChecker Shared Test Library via the Import API.

.DESCRIPTION
    Downloads or reads a local copy of the Atomic Red Team YAML repository and
    POSTs each test as an Approved SharedTestDefinition to the ShieldChecker Import API.
    Existing entries are matched by ExternalId (the ART GUID) and
    skipped unless -Update is specified.

    Authentication uses the OAuth 2.0 client-credentials flow against the
    ShieldChecker-ImportApi app registration.  No SQL connection is required.

.PARAMETER ImportApiBaseUrl
    Base URL of the ShieldChecker Import API container.
    Example: "https://ca-shieldchecker-importapi-prd-001.blueocean.azurecontainerapps.io"

.PARAMETER TenantId
    Azure AD tenant ID.

.PARAMETER ClientId
    Client ID of the operator's dedicated app registration that holds the
    Import.SharedLibrary AppRole on ShieldChecker-ImportApi.

.PARAMETER ClientSecret
    Client secret for the app registration.

.PARAMETER ImportApiClientId
    Client ID of the ShieldChecker-ImportApi app registration.
    Used to build the OAuth2 scope: api://{ImportApiClientId}/.default

.PARAMETER AtomicRedTeamPath
    Path to a local checkout of the Atomic Red Team repository.
    If omitted, the YAML files are downloaded from GitHub (-DownloadFromGitHub
    is implied).

.PARAMETER DownloadFromGitHub
    When specified (or when AtomicRedTeamPath is omitted), downloads the
    Atomic Red Team YAML index from GitHub and parses each technique.

.PARAMETER Update
    When specified, existing Approved entries are updated with the latest
    field values from the YAML source.

.PARAMETER WhatIf
    Runs in dry-run mode; no API writes are performed.

.EXAMPLE
    # Import from GitHub, create new entries only
    .\Import-AtomicRedTeamToSharedLibrary.ps1 `
        -ImportApiBaseUrl "https://ca-shieldchecker-importapi-prd-001.example.azurecontainerapps.io" `
        -TenantId "00000000-0000-0000-0000-000000000000" `
        -ClientId "11111111-0000-0000-0000-000000000000" `
        -ClientSecret "..." `
        -ImportApiClientId "22222222-0000-0000-0000-000000000000" `
        -DownloadFromGitHub

.EXAMPLE
    # Import from local checkout and update existing entries
    .\Import-AtomicRedTeamToSharedLibrary.ps1 `
        -ImportApiBaseUrl "https://..." `
        -TenantId "..." -ClientId "..." -ClientSecret "..." `
        -ImportApiClientId "..." `
        -AtomicRedTeamPath "C:\atomics" `
        -Update

.NOTES
    Requires: powershell-yaml module  (Install-Module powershell-yaml)
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$ImportApiBaseUrl,

    [Parameter(Mandatory)]
    [string]$TenantId,

    [Parameter(Mandatory)]
    [string]$ClientId,

    [Parameter(Mandatory)]
    [string]$ClientSecret,

    [Parameter(Mandatory)]
    [string]$ImportApiClientId,

    [Parameter(ParameterSetName = 'Local')]
    [string]$AtomicRedTeamPath,

    [Parameter(ParameterSetName = 'GitHub')]
    [switch]$DownloadFromGitHub,

    [switch]$Update
)

#Requires -Modules powershell-yaml

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Acquire OAuth 2.0 client-credentials token
# ---------------------------------------------------------------------------
function Get-AccessToken {
    param([string]$TenantId, [string]$ClientId, [string]$ClientSecret, [string]$Scope)

    $tokenUrl = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token"
    $body = @{
        grant_type    = 'client_credentials'
        client_id     = $ClientId
        client_secret = $ClientSecret
        scope         = $Scope
    }
    $response = Invoke-RestMethod -Method Post -Uri $tokenUrl -Body $body -ContentType 'application/x-www-form-urlencoded'
    return $response.access_token
}

$scope = "api://$ImportApiClientId/.default"
Write-Host "Acquiring access token for scope: $scope"
$accessToken = Get-AccessToken -TenantId $TenantId -ClientId $ClientId -ClientSecret $ClientSecret -Scope $scope
Write-Host "Token acquired successfully."

# ---------------------------------------------------------------------------
# Helper: map ART executor name → ShieldChecker ExecutorSystemType int value
# ---------------------------------------------------------------------------
function ConvertTo-ExecutorSystemType([string]$Executor) {
    switch ($Executor.ToLower()) {
        'command_prompt' { return 0 }  # Worker
        'powershell'     { return 0 }  # Worker
        'sh'             { return 0 }  # Worker
        'bash'           { return 0 }  # Worker
        default          { return 0 }  # Worker (fallback)
    }
}

# ---------------------------------------------------------------------------
# Helper: map ART supported_platforms → OperatingSystem int value
# ---------------------------------------------------------------------------
function ConvertTo-OperatingSystem([string[]]$Platforms) {
    if ($Platforms -contains 'linux' -or $Platforms -contains 'macos') {
        return 1  # Linux
    }
    return 0  # Windows
}

# ---------------------------------------------------------------------------
# Gather YAML file paths
# ---------------------------------------------------------------------------
$yamlFiles = @()

if ($PSCmdlet.ParameterSetName -eq 'Local' -and $AtomicRedTeamPath) {
    Write-Host "Reading YAML files from: $AtomicRedTeamPath"
    $yamlFiles = Get-ChildItem -Path $AtomicRedTeamPath -Recurse -Filter '*.yaml' |
        Where-Object { $_.Name -match '^T\d+' }
} else {
    Write-Host "Downloading Atomic Red Team index from GitHub..."
    $indexUrl  = 'https://raw.githubusercontent.com/redcanaryco/atomic-red-team/master/atomics/Indexes/index.yaml'
    $indexYaml = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing
    $index     = ConvertFrom-Yaml $indexYaml

    $tmpDir = Join-Path $env:TEMP 'AtomicRedTeam'
    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null

    foreach ($techniqueId in $index.Keys) {
        $url      = "https://raw.githubusercontent.com/redcanaryco/atomic-red-team/master/atomics/$techniqueId/$techniqueId.yaml"
        $filePath = Join-Path $tmpDir "$techniqueId.yaml"
        try {
            Invoke-WebRequest -Uri $url -OutFile $filePath -UseBasicParsing -ErrorAction Stop
            $yamlFiles += Get-Item $filePath
        } catch {
            Write-Warning "Could not download $techniqueId : $_"
        }
    }
}

Write-Host "Found $($yamlFiles.Count) YAML file(s) to process."

# ---------------------------------------------------------------------------
# Build the batch payload
# ---------------------------------------------------------------------------
$tests = [System.Collections.Generic.List[hashtable]]::new()

foreach ($file in $yamlFiles) {
    try {
        $raw  = Get-Content $file.FullName -Raw
        $data = ConvertFrom-Yaml $raw

        $mitreTechnique = $data.'attack_technique' ?? ''

        foreach ($atomicTest in $data.'atomic_tests') {
            $guid        = $atomicTest.'auto_generated_guid' ?? [guid]::NewGuid().ToString()
            $name        = $atomicTest.'name'                ?? 'Unnamed'
            $description = $atomicTest.'description'         ?? ''
            $executor    = $atomicTest.'executor'
            $platforms   = [string[]]($atomicTest.'supported_platforms' ?? @('windows'))

            $execType  = ConvertTo-ExecutorSystemType ($executor.'name' ?? '')
            $osType    = ConvertTo-OperatingSystem $platforms
            $scriptTest = ($executor.'command' ?? '') -replace '\r?\n', "`n"

            # Truncate to column limits
            if ($name.Length -gt 150)           { $name           = $name.Substring(0, 150) }
            if ($mitreTechnique.Length -gt 16)  { $mitreTechnique = $mitreTechnique.Substring(0, 16) }

            $tests.Add(@{
                externalId        = $guid
                name              = $name
                mitreTechnique    = $mitreTechnique
                description       = $description
                scriptTest        = $scriptTest
                operatingSystem   = $osType
                executorSystemType = $execType
            })
        }
    } catch {
        Write-Warning "Error parsing $($file.Name): $_"
    }
}

Write-Host "Parsed $($tests.Count) test(s) from YAML files."

# ---------------------------------------------------------------------------
# POST to Import API  (batch in chunks of 200 to stay within request limits)
# ---------------------------------------------------------------------------
$batchSize   = 200
$totalTests  = $tests.Count
$batchCount  = [math]::Ceiling($totalTests / $batchSize)
$grandStats  = @{ Inserted = 0; Updated = 0; Skipped = 0; Errors = 0 }

$headers = @{
    'Authorization' = "Bearer $accessToken"
    'Content-Type'  = 'application/json'
}

$importUrl = "$($ImportApiBaseUrl.TrimEnd('/'))/api/sharedlibrary/import"

for ($i = 0; $i -lt $batchCount; $i++) {
    $batchTests = $tests | Select-Object -Skip ($i * $batchSize) -First $batchSize

    $payload = @{
        update = $Update.IsPresent
        tests  = @($batchTests)
    } | ConvertTo-Json -Depth 10

    if ($PSCmdlet.ShouldProcess("Batch $($i+1)/$batchCount ($($batchTests.Count) tests)", 'POST to Import API')) {
        try {
            $response = Invoke-RestMethod -Method Post -Uri $importUrl -Headers $headers -Body $payload -ErrorAction Stop
            $grandStats.Inserted += $response.inserted
            $grandStats.Updated  += $response.updated
            $grandStats.Skipped  += $response.skipped
            $grandStats.Errors   += $response.errors
            Write-Host "Batch $($i+1)/$batchCount: Inserted=$($response.inserted) Updated=$($response.updated) Skipped=$($response.skipped) Errors=$($response.errors)"
        } catch {
            Write-Warning "Batch $($i+1)/$batchCount failed: $_"
            $grandStats.Errors += $batchTests.Count
        }
    }
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Import Summary ==="
Write-Host "  Inserted: $($grandStats.Inserted)"
Write-Host "  Updated : $($grandStats.Updated)"
Write-Host "  Skipped : $($grandStats.Skipped)"
Write-Host "  Errors  : $($grandStats.Errors)"

