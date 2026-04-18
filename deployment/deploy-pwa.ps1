#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build the SafeExchange Blazor WASM PWA in Release and upload it
    to the Azure hosting target configured in deployment/.env.

.DESCRIPTION
    Wrapper around `dotnet publish` + an Azure upload command whose
    exact form depends on HOSTING_TYPE_<ENV> in deployment/.env:

      storage       → az storage blob upload-batch into the $web
                      container of STORAGE_ACCOUNT_<ENV>
      staticwebapp  → swa deploy to STATIC_WEB_APP_<ENV>
      appservice    → az webapp deploy a zip to APP_SERVICE_<ENV>

    All hosting details (account name, resource group, subscription,
    Front Door names) live in deployment/.env which is gitignored.
    The committed .env.example documents every variable the operator
    must set.

    After a successful upload, if FRONT_DOOR_PROFILE_<ENV> and
    FRONT_DOOR_ENDPOINT_<ENV> are both set, the script purges the
    AFD cache so the new index.html (with updated CSP) is picked up
    immediately rather than waiting out the TTL.

.PARAMETER Environment
    Which environment to deploy. 'test' maps to *_TEST variables,
    'prd' to *_PRD variables.

.PARAMETER WhatIf
    Build and stage the output but skip the upload and cache purge.
    Useful for verifying the publish output without touching cloud
    state.

.PARAMETER EnvFile
    Override the path to the .env file. Defaults to
    deployment/.env alongside this script.

.EXAMPLE
    ./deployment/deploy-pwa.ps1 -Environment test

    Publishes Release, uploads to the configured test target, and
    purges the Front Door cache if FRONT_DOOR_* variables are set.

.EXAMPLE
    ./deployment/deploy-pwa.ps1 -Environment prd -WhatIf

    Publishes Release but does not upload or purge. Prints what
    the upload command would have been.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('test', 'prd')]
    [string]$Environment,

    [Parameter()]
    [switch]$WhatIf,

    [Parameter()]
    [string]$EnvFile
)

# Disable Git Bash MSYS path translation for az CLI calls — without
# this, leading '/' on Azure resource IDs gets rewritten as
# 'C:/Program Files/Git/' when `az` runs from inside a bash shell
# on Windows.
$env:MSYS_NO_PATHCONV = '1'

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# ──────────────────────────────────────────────────────────────────
# Paths
# ──────────────────────────────────────────────────────────────────
$here       = $PSScriptRoot
$repoRoot   = Split-Path -Parent $here
$pwaProject = Join-Path $repoRoot 'SafeExchange.PWA/SafeExchange.PWA.csproj'

# Pin the publish output to a dedicated folder under the repo so it can't
# drift with future SDK conventions (net7 used bin/.../browser-wasm/publish,
# net8 default is bin/.../publish). Passed to `dotnet publish -o`.
$publishRoot = Join-Path $repoRoot 'SafeExchange.PWA/bin/Release/pwa-publish'
$publishDir  = Join-Path $publishRoot 'wwwroot'

if (-not $EnvFile) {
    $EnvFile = Join-Path $here '.env'
}

if (-not (Test-Path $pwaProject)) {
    throw "PWA project file not found: $pwaProject"
}

# ──────────────────────────────────────────────────────────────────
# .env reader
# ──────────────────────────────────────────────────────────────────
# Reads a single KEY=value line out of a simple .env file. Returns
# empty string if the file or key is missing. Trims surrounding
# whitespace and optional single/double quotes.
function Read-EnvValue {
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path $Path)) {
        return ''
    }

    $pattern = '^' + [regex]::Escape($Name) + '\s*=\s*(.*)$'
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }
        if ($trimmed -match $pattern) {
            return $matches[1].Trim().Trim('"').Trim("'")
        }
    }
    return ''
}

function Require-EnvValue {
    param(
        [string]$Path,
        [string]$Name
    )

    $value = Read-EnvValue -Path $Path -Name $Name
    if (-not $value) {
        throw "Required variable '$Name' is not set in $Path. See deployment/.env.example for the full list."
    }
    return $value
}

# ──────────────────────────────────────────────────────────────────
# Resolve env-specific values
# ──────────────────────────────────────────────────────────────────
$envSuffix    = $Environment.ToUpperInvariant()
$hostingType  = (Require-EnvValue -Path $EnvFile -Name "HOSTING_TYPE_$envSuffix").ToLowerInvariant()
$resourceGroup = Require-EnvValue -Path $EnvFile -Name "RESOURCE_GROUP_$envSuffix"

$validTypes = @('storage', 'staticwebapp', 'appservice')
if ($hostingType -notin $validTypes) {
    throw "HOSTING_TYPE_$envSuffix must be one of: $($validTypes -join ', '). Got '$hostingType'."
}

$subscription = Read-EnvValue -Path $EnvFile -Name 'SUBSCRIPTION'

Write-Host "Environment:   $Environment" -ForegroundColor DarkGray
Write-Host "Hosting type:  $hostingType" -ForegroundColor DarkGray
Write-Host "Resource grp:  $resourceGroup" -ForegroundColor DarkGray
if ($subscription) {
    Write-Host "Subscription:  $subscription" -ForegroundColor DarkGray
}

# ──────────────────────────────────────────────────────────────────
# Sanity-check az CLI + login state before any build or upload
# ──────────────────────────────────────────────────────────────────
try {
    $null = & az --version 2>&1
} catch {
    throw "Azure CLI ('az') is not installed or not on PATH. Install from https://aka.ms/azcli and try again."
}

$accountJson = az account show 2>$null
if (-not $accountJson) {
    throw "Not signed in to Azure. Run 'az login' first."
}
$account = $accountJson | ConvertFrom-Json
Write-Host "Signed in as $($account.user.name), subscription $($account.name) ($($account.id))" -ForegroundColor DarkGray

if ($subscription -and $subscription -ne $account.id) {
    Write-Host "Switching to subscription $subscription..." -ForegroundColor DarkGray
    az account set --subscription $subscription
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to switch subscription to $subscription."
    }
}

# ──────────────────────────────────────────────────────────────────
# Build and publish
# ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Publishing $pwaProject (Release)..." -ForegroundColor Cyan

# Wipe the pinned output folder so stale artefacts from a previous publish
# can't sneak into the upload.
if (Test-Path $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

& dotnet publish $pwaProject -c Release -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $publishDir)) {
    throw "Publish output not found at expected location: $publishDir"
}
Write-Host "Publish output: $publishDir" -ForegroundColor DarkGray

# ──────────────────────────────────────────────────────────────────
# Inject env-specific appsettings.json values
# ──────────────────────────────────────────────────────────────────
# Source appsettings.json ships staging-dev defaults so `dotnet run`
# works locally. Real per-env values (tenant, clientId, backend URL,
# API scope) live only in deployment/.env and are merged into the
# published appsettings.json here, before it reaches $web. This
# prevents prod credentials from sitting in git while still letting
# the deployed bundle be a normal static asset.
$appsettingsAuthority = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_AUTHORITY_$envSuffix"
$appsettingsClientId  = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_CLIENT_ID_$envSuffix"
$appsettingsBackend   = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_BACKEND_$envSuffix"
$appsettingsScope     = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_SCOPE_$envSuffix"

# Telemetry is opt-in per env. When APPSETTINGS_TELEMETRY_ENABLED_<ENV>
# is absent or false, the Telemetry section in appsettings stays at its
# source defaults (Enabled=false, empty connection string). The C#
# TelemetryService honours the flag by never calling the AI JS SDK
# initialiser, so no data can leave the browser.
$appsettingsTelemetryEnabledRaw = Read-EnvValue -Path $EnvFile -Name "APPSETTINGS_TELEMETRY_ENABLED_$envSuffix"
$appsettingsTelemetryEnabled    = $false
if ($appsettingsTelemetryEnabledRaw) {
    $appsettingsTelemetryEnabled = [System.Convert]::ToBoolean($appsettingsTelemetryEnabledRaw)
}
$appsettingsTelemetryConnection = Read-EnvValue -Path $EnvFile -Name "APPSETTINGS_TELEMETRY_CONNECTION_STRING_$envSuffix"
if ($appsettingsTelemetryEnabled -and -not $appsettingsTelemetryConnection) {
    throw "APPSETTINGS_TELEMETRY_ENABLED_$envSuffix is true but APPSETTINGS_TELEMETRY_CONNECTION_STRING_$envSuffix is empty."
}

$publishAppsettings = Join-Path $publishDir 'appsettings.json'
if (-not (Test-Path $publishAppsettings)) {
    throw "Published appsettings.json not found at $publishAppsettings"
}

Write-Host "Rewriting $publishAppsettings with $envSuffix overrides..." -ForegroundColor DarkGray
$settings = Get-Content -LiteralPath $publishAppsettings -Raw | ConvertFrom-Json
$settings.AzureAdB2C.Authority       = $appsettingsAuthority
$settings.AzureAdB2C.ClientId        = $appsettingsClientId
$settings.BackendApi.BaseAddress     = $appsettingsBackend
$settings.AccessTokenScopes          = @($appsettingsScope)
$settings.Telemetry.Enabled          = $appsettingsTelemetryEnabled
$settings.Telemetry.ConnectionString = if ($appsettingsTelemetryEnabled) { $appsettingsTelemetryConnection } else { '' }
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $publishAppsettings -NoNewline
Write-Host "  ClientId:         $appsettingsClientId" -ForegroundColor DarkGray
Write-Host "  Authority:        $appsettingsAuthority" -ForegroundColor DarkGray
Write-Host "  BackendApi:       $appsettingsBackend" -ForegroundColor DarkGray
Write-Host "  Scope:            $appsettingsScope" -ForegroundColor DarkGray
Write-Host "  Telemetry.Enabled: $appsettingsTelemetryEnabled" -ForegroundColor DarkGray

if ($WhatIf) {
    Write-Host ""
    Write-Host "What-if: skipping upload and cache purge. Nothing was uploaded." -ForegroundColor Yellow
    exit 0
}

# ──────────────────────────────────────────────────────────────────
# Upload — branches on HOSTING_TYPE
# ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Uploading to $hostingType target..." -ForegroundColor Cyan

switch ($hostingType) {
    'storage' {
        $storageAccount = Require-EnvValue -Path $EnvFile -Name "STORAGE_ACCOUNT_$envSuffix"
        Write-Host "  Storage account: $storageAccount" -ForegroundColor DarkGray

        # az storage blob upload-batch uploads every file under
        # $publishDir into the $web container. --overwrite keeps
        # stale assets around only if they no longer exist in the
        # new publish output; the --delete-destination-path flag
        # would remove those, but it's not available on every az
        # version, so the caller should manually clear $web if it
        # matters for a given deploy.
        # AAD auth — requires the operator to hold "Storage Blob Data
        # Contributor" on the storage account. Paired with
        # allowSharedKeyAccess=false on the account so shared-key
        # upload paths cannot be used as a fallback. See
        # deployment/README when configuring a new environment.
        $azArgs = @(
            'storage', 'blob', 'upload-batch'
            '--account-name',   $storageAccount
            '--destination',    '$web'
            '--source',         $publishDir
            '--overwrite',      'true'
            '--auth-mode',      'login'
        )
        & az @azArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Storage upload failed with exit code $LASTEXITCODE"
        }
    }

    'staticwebapp' {
        $staticWebApp = Require-EnvValue -Path $EnvFile -Name "STATIC_WEB_APP_$envSuffix"
        Write-Host "  Static Web App: $staticWebApp" -ForegroundColor DarkGray

        # Fetch a one-time deployment token for this SWA. Stored in
        # a variable rather than on the command line so it does not
        # show up in PowerShell's history.
        $deployToken = az staticwebapp secrets list `
            --name $staticWebApp `
            --resource-group $resourceGroup `
            --query "properties.apiKey" -o tsv
        if (-not $deployToken) {
            throw "Failed to fetch deployment token for SWA '$staticWebApp'."
        }

        # SWA CLI uploads the content. Requires `npm install -g @azure/static-web-apps-cli`.
        try {
            $null = & swa --version 2>&1
        } catch {
            throw "Static Web Apps CLI ('swa') is not installed. Run 'npm install -g @azure/static-web-apps-cli' and try again."
        }

        & swa deploy $publishDir --deployment-token $deployToken --env production
        if ($LASTEXITCODE -ne 0) {
            throw "swa deploy failed with exit code $LASTEXITCODE"
        }
    }

    'appservice' {
        $appService = Require-EnvValue -Path $EnvFile -Name "APP_SERVICE_$envSuffix"
        Write-Host "  App Service: $appService" -ForegroundColor DarkGray

        # Build a zip of the publish output and push it with
        # `az webapp deploy --type zip`. The destination path inside
        # the app is /home/site/wwwroot by default.
        $zipPath = Join-Path ([System.IO.Path]::GetTempPath()) `
            ("safeexchange-pwa-" + [System.Guid]::NewGuid().ToString('N') + ".zip")

        try {
            Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

            $azArgs = @(
                'webapp', 'deploy'
                '--name',             $appService
                '--resource-group',   $resourceGroup
                '--src-path',         $zipPath
                '--type',             'zip'
            )
            & az @azArgs
            if ($LASTEXITCODE -ne 0) {
                throw "App Service deploy failed with exit code $LASTEXITCODE"
            }
        } finally {
            Remove-Item -LiteralPath $zipPath -ErrorAction SilentlyContinue
        }
    }
}

# ──────────────────────────────────────────────────────────────────
# Optional: purge Front Door cache
# ──────────────────────────────────────────────────────────────────
# AFD profiles typically live in a shared networking/DNS resource
# group that is different from the storage RG, so the purge needs
# its own FRONT_DOOR_RG_<ENV>. Falls back to the storage RG only
# when FRONT_DOOR_RG_* is not set — keeps the scaffold compatible
# with single-RG setups.
$afdProfile  = Read-EnvValue -Path $EnvFile -Name "FRONT_DOOR_PROFILE_$envSuffix"
$afdEndpoint = Read-EnvValue -Path $EnvFile -Name "FRONT_DOOR_ENDPOINT_$envSuffix"
$afdRg       = Read-EnvValue -Path $EnvFile -Name "FRONT_DOOR_RG_$envSuffix"
if (-not $afdRg) { $afdRg = $resourceGroup }

if ($afdProfile -and $afdEndpoint) {
    Write-Host ""
    Write-Host "Purging Front Door cache ($afdProfile / $afdEndpoint in $afdRg)..." -ForegroundColor Cyan
    $azArgs = @(
        'afd', 'endpoint', 'purge'
        '--profile-name',     $afdProfile
        '--endpoint-name',    $afdEndpoint
        '--resource-group',   $afdRg
        '--content-paths',    '/*'
    )
    & az @azArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Front Door purge failed with exit code $LASTEXITCODE. The upload succeeded; clients may see cached content until TTL expires."
    }
} else {
    Write-Host ""
    Write-Host "FRONT_DOOR_* not set for $envSuffix — skipping cache purge." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Deployment to $Environment complete." -ForegroundColor Green
