<#
.SYNOPSIS
    Imports Atomic Red Team tests into the ShieldChecker Shared Test Library.

.DESCRIPTION
    Downloads or reads a local copy of the Atomic Red Team YAML repository and
    inserts each test as an Approved SharedTestDefinition in the ShieldChecker
    database.  Existing entries are matched by ExternalId (the ART GUID) and
    skipped unless -Update is specified.

.PARAMETER ConnectionString
    Azure SQL connection string for the ShieldChecker database.
    Example: "Server=tcp:myserver.database.windows.net;Database=shieldchecker;
              Authentication=Active Directory Default;"

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
    Runs in dry-run mode; no database writes are performed.

.EXAMPLE
    # Import from GitHub, create new entries only
    .\Import-AtomicRedTeamToSharedLibrary.ps1 -ConnectionString "..." -DownloadFromGitHub

.EXAMPLE
    # Import from local checkout and update existing entries
    .\Import-AtomicRedTeamToSharedLibrary.ps1 `
        -ConnectionString "..." `
        -AtomicRedTeamPath "C:\atomics" `
        -Update

.NOTES
    Requires: SqlServer PowerShell module  (Install-Module SqlServer)
              powershell-yaml module        (Install-Module powershell-yaml)
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$ConnectionString,

    [Parameter(ParameterSetName = 'Local')]
    [string]$AtomicRedTeamPath,

    [Parameter(ParameterSetName = 'GitHub')]
    [switch]$DownloadFromGitHub,

    [switch]$Update
)

#Requires -Modules SqlServer, powershell-yaml

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
# Statistics
# ---------------------------------------------------------------------------
$stats = @{ Parsed = 0; Inserted = 0; Updated = 0; Skipped = 0; Errors = 0 }

# ---------------------------------------------------------------------------
# Process each YAML file
# ---------------------------------------------------------------------------
foreach ($file in $yamlFiles) {
    try {
        $raw  = Get-Content $file.FullName -Raw
        $data = ConvertFrom-Yaml $raw

        $mitreTechnique = $data.'attack_technique' ?? ''

        foreach ($atomicTest in $data.'atomic_tests') {
            $stats.Parsed++

            $guid        = $atomicTest.'auto_generated_guid' ?? [guid]::NewGuid().ToString()
            $name        = $atomicTest.'name'                ?? 'Unnamed'
            $description = $atomicTest.'description'         ?? ''
            $executor    = $atomicTest.'executor'
            $platforms   = [string[]]($atomicTest.'supported_platforms' ?? @('windows'))

            $execType  = ConvertTo-ExecutorSystemType ($executor.'name' ?? '')
            $osType    = ConvertTo-OperatingSystem $platforms
            $scriptTest = ($executor.'command' ?? '') -replace '\r?\n', "`n"

            # Truncate name and mitre to column limits
            if ($name.Length -gt 150)           { $name           = $name.Substring(0, 150) }
            if ($mitreTechnique.Length -gt 16)  { $mitreTechnique = $mitreTechnique.Substring(0, 16) }

            # Check for existing entry
            $checkQuery = "SELECT ID, Status FROM SharedTestDefinition WHERE ExternalId = @ExternalId"
            $existingRow = Invoke-Sqlcmd -ConnectionString $ConnectionString `
                                          -Query $checkQuery `
                                          -Variable @("ExternalId=$guid") `
                                          -ErrorAction Stop

            if ($existingRow) {
                if (-not $Update) {
                    $stats.Skipped++
                    continue
                }
                # Update existing
                $updateQuery = @"
UPDATE SharedTestDefinition
SET    Name               = @Name,
       MitreTechnique     = @MitreTechnique,
       Description        = @Description,
       ScriptTest         = @ScriptTest,
       ScriptPrerequisites= '',
       ScriptCleanup      = '',
       OperatingSystem    = @OperatingSystem,
       ExecutorSystemType = @ExecutorSystemType,
       ExecutorUserType   = 0,
       ElevationRequired  = 0
WHERE  ExternalId = @ExternalId
"@
                if ($PSCmdlet.ShouldProcess($name, 'Update SharedTestDefinition')) {
                    Invoke-Sqlcmd -ConnectionString $ConnectionString `
                                  -Query $updateQuery `
                                  -Variable @(
                                      "Name=$name",
                                      "MitreTechnique=$mitreTechnique",
                                      "Description=$description",
                                      "ScriptTest=$scriptTest",
                                      "OperatingSystem=$osType",
                                      "ExecutorSystemType=$execType",
                                      "ExternalId=$guid"
                                  ) -ErrorAction Stop
                    $stats.Updated++
                }
            } else {
                # Insert new entry as Approved
                $insertQuery = @"
INSERT INTO SharedTestDefinition
    (Name, MitreTechnique, Description, ExpectedAlertTitle,
     ScriptTest, ScriptPrerequisites, ScriptCleanup,
     ElevationRequired, OperatingSystem, ExecutorSystemType,
     ExecutorUserType, ExternalId, Status,
     SubmittedBy_Id, SubmittedAt)
SELECT TOP 1
    @Name, @MitreTechnique, @Description, 'Unknown',
    @ScriptTest, '', '',
    0, @OperatingSystem, @ExecutorSystemType,
    0, @ExternalId, 1,
    Id, GETUTCDATE()
FROM   UserInfo
ORDER BY Id
"@
                if ($PSCmdlet.ShouldProcess($name, 'Insert SharedTestDefinition')) {
                    Invoke-Sqlcmd -ConnectionString $ConnectionString `
                                  -Query $insertQuery `
                                  -Variable @(
                                      "Name=$name",
                                      "MitreTechnique=$mitreTechnique",
                                      "Description=$description",
                                      "ScriptTest=$scriptTest",
                                      "OperatingSystem=$osType",
                                      "ExecutorSystemType=$execType",
                                      "ExternalId=$guid"
                                  ) -ErrorAction Stop
                    $stats.Inserted++
                }
            }
        }
    } catch {
        Write-Warning "Error processing $($file.Name): $_"
        $stats.Errors++
    }
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Import Summary ==="
Write-Host "  Parsed  : $($stats.Parsed)"
Write-Host "  Inserted: $($stats.Inserted)"
Write-Host "  Updated : $($stats.Updated)"
Write-Host "  Skipped : $($stats.Skipped)"
Write-Host "  Errors  : $($stats.Errors)"
