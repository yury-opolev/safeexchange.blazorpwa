#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build the SafeExchange Blazor WASM PWA in Release and upload it
    to the Azure hosting target configured in deployment/.env.

.DESCRIPTION
    Wrapper around `dotnet publish` + an Azure upload command whose
    exact form depends on HOSTING_TYPE_<ENV> in deployment/.env:

      storage       -> az storage blob upload-batch into the $web
                      container of STORAGE_ACCOUNT_<ENV>
      staticwebapp  -> swa deploy to STATIC_WEB_APP_<ENV>
      appservice    -> az webapp deploy a zip to APP_SERVICE_<ENV>

    Per-env appsettings values (tenant, clientId, backend URL, API
    scope) live only in deployment/.env and are injected into the
    SOURCE wwwroot/appsettings.json BEFORE `dotnet publish`. This is
    deliberate: dotnet publish hash-derives several files from
    appsettings.json (the precompressed appsettings.json.gz/.br and
    the SHA-256 baked into service-worker-assets.js). Injecting after
    publish — as an earlier version did — left those derived files
    holding the source staging-dev values, so browsers that request
    gzip/br got stale config. The source working tree is restored
    after publish so secrets never linger on disk or in git.

    All hosting details (account name, resource group, subscription,
    Front Door names) live in deployment/.env which is gitignored.
    The committed .env.example documents every variable the operator
    must set.

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

    Publishes Release but does not upload or purge.
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

# ------------------------------------------------------------------
# Paths
# ------------------------------------------------------------------
$here       = $PSScriptRoot
$repoRoot   = Split-Path -Parent $here
$pwaProject = Join-Path $repoRoot 'SafeExchange.PWA/SafeExchange.PWA.csproj'

# Pin the publish output to a dedicated folder under the repo so it can't
# drift with future SDK conventions (net7 used bin/.../browser-wasm/publish,
# net8 default is bin/.../publish). Passed to `dotnet publish -o`.
$publishRoot = Join-Path $repoRoot 'SafeExchange.PWA/bin/Release/pwa-publish'
$publishDir  = Join-Path $publishRoot 'wwwroot'

# Source files mutated in place before publish and restored afterwards.
$srcAppsettings = Join-Path $repoRoot 'SafeExchange.PWA/wwwroot/appsettings.json'
$srcVersionJson = Join-Path $repoRoot 'SafeExchange.PWA/wwwroot/version.json'

if (-not $EnvFile) {
    $EnvFile = Join-Path $here '.env'
}

if (-not (Test-Path $pwaProject)) {
    throw "PWA project file not found: $pwaProject"
}

# ------------------------------------------------------------------
# .env reader
# ------------------------------------------------------------------
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

# ------------------------------------------------------------------
# Resolve env-specific values
# ------------------------------------------------------------------
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

# ------------------------------------------------------------------
# Sanity-check az CLI + login state before any build or upload
# ------------------------------------------------------------------
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

# ------------------------------------------------------------------
# Auto-bump version.json patch number
# ------------------------------------------------------------------
# Every deploy = a new build, so bump the patch number in
# SafeExchange.PWA/wwwroot/version.json before publishing.
#
# To avoid double-bumping when staging and prod are deployed
# back-to-back for the same code, the bump is SKIPPED if HEAD already
# changed the "version" field. Skipped entirely under -WhatIf.
$bumpedThisDeploy = $false
if ((-not $WhatIf) -and (Test-Path $srcVersionJson)) {
    $relPath  = 'SafeExchange.PWA/wwwroot/version.json'
    $headDiff = git -C $repoRoot show HEAD --format='' -- $relPath 2>&1
    $versionAlreadyChanged = @($headDiff | Where-Object { $_ -match '^\+\s*"version"\s*:' }).Count -gt 0

    if ($versionAlreadyChanged) {
        $current = (Get-Content -LiteralPath $srcVersionJson -Raw | ConvertFrom-Json).version
        Write-Host ""
        Write-Host "Version: $current (HEAD already bumped this; no auto-bump for this deploy)" -ForegroundColor DarkGray
    } else {
        $info  = Get-Content -LiteralPath $srcVersionJson -Raw | ConvertFrom-Json
        $parts = $info.version -split '\.'
        if ($parts.Length -lt 3) {
            Write-Warning "version.json version '$($info.version)' is not MAJOR.MINOR.PATCH; skipping auto-bump."
        } else {
            $oldVersion = $info.version
            $parts[2]   = [string]([int]$parts[2] + 1)
            $info.version = $parts -join '.'
            $info | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $srcVersionJson -NoNewline

            git -C $repoRoot add $relPath | Out-Null
            git -C $repoRoot commit -m "chore(version): auto-bump $oldVersion -> $($info.version) before $Environment deploy" -q
            if ($LASTEXITCODE -eq 0) {
                $bumpedThisDeploy = $true
                Write-Host ""
                Write-Host "Auto-bumped version: $oldVersion -> $($info.version) (committed locally; pushed after successful deploy)" -ForegroundColor Cyan
            } else {
                Write-Warning "Auto-bump commit failed (exit $LASTEXITCODE). The bumped version is on disk; resolve manually."
            }
        }
    }
}

# ------------------------------------------------------------------
# Inject env-specific appsettings.json values BEFORE publish
# ------------------------------------------------------------------
# Source appsettings.json ships staging-dev defaults so `dotnet run`
# works locally. The real per-env values (Authority, ClientId, backend
# URL, API scope) come from deployment/.env and are written into the
# SOURCE file here, before publish, so dotnet computes appsettings.json
# .gz/.br and the SW integrity hash from the correct content. Today's
# build date is stamped into version.json the same way. Both source
# files are backed up and restored in the finally below.
$appsettingsAuthority = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_AUTHORITY_$envSuffix"
$appsettingsClientId  = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_CLIENT_ID_$envSuffix"
$appsettingsBackend   = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_BACKEND_$envSuffix"
$appsettingsScope     = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_SCOPE_$envSuffix"

if (-not (Test-Path $srcAppsettings)) {
    throw "Source appsettings.json not found: $srcAppsettings"
}

$appsettingsBackup = "$srcAppsettings.deploy-bak"
$versionBackup     = if (Test-Path $srcVersionJson) { "$srcVersionJson.deploy-bak" } else { $null }
Copy-Item -LiteralPath $srcAppsettings -Destination $appsettingsBackup -Force
if ($versionBackup) { Copy-Item -LiteralPath $srcVersionJson -Destination $versionBackup -Force }

Write-Host ""
Write-Host "Injecting $envSuffix appsettings into source before publish..." -ForegroundColor Cyan
$settings = Get-Content -LiteralPath $srcAppsettings -Raw | ConvertFrom-Json
$settings.AzureAdB2C.Authority   = $appsettingsAuthority
$settings.AzureAdB2C.ClientId    = $appsettingsClientId
$settings.BackendApi.BaseAddress = $appsettingsBackend
$settings.AccessTokenScopes      = @($appsettingsScope)
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $srcAppsettings -NoNewline
Write-Host "  ClientId:   $appsettingsClientId" -ForegroundColor DarkGray
Write-Host "  Authority:  $appsettingsAuthority" -ForegroundColor DarkGray
Write-Host "  BackendApi: $appsettingsBackend" -ForegroundColor DarkGray
Write-Host "  Scope:      $appsettingsScope" -ForegroundColor DarkGray

if (Test-Path $srcVersionJson) {
    $versionInfo = Get-Content -LiteralPath $srcVersionJson -Raw | ConvertFrom-Json
    $versionInfo.buildDate = [System.DateTime]::UtcNow.ToString('yyyy-MM-dd')
    $versionInfo | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $srcVersionJson -NoNewline
    Write-Host "  version:    v$($versionInfo.version) ($($versionInfo.buildDate))" -ForegroundColor DarkGray
}

# ------------------------------------------------------------------
# Build and publish (source restored in finally)
# ------------------------------------------------------------------
Write-Host ""
Write-Host "Publishing $pwaProject (Release)..." -ForegroundColor Cyan
try {
    # Wipe the pinned output folder so stale artefacts from a previous
    # publish can't sneak into the upload.
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
}
finally {
    # Restore the working tree: revert injected secrets + build date so
    # nothing sensitive lingers on disk or sneaks into a later commit.
    Copy-Item -LiteralPath $appsettingsBackup -Destination $srcAppsettings -Force
    Remove-Item -LiteralPath $appsettingsBackup -Force
    if ($versionBackup) {
        Copy-Item -LiteralPath $versionBackup -Destination $srcVersionJson -Force
        Remove-Item -LiteralPath $versionBackup -Force
    }
    Write-Host "Restored source appsettings.json / version.json." -ForegroundColor DarkGray
}

if ($WhatIf) {
    Write-Host ""
    Write-Host "What-if: skipping upload and cache purge. Nothing was uploaded." -ForegroundColor Yellow
    exit 0
}

# ------------------------------------------------------------------
# Upload — branches on HOSTING_TYPE
# ------------------------------------------------------------------
Write-Host ""
Write-Host "Uploading to $hostingType target..." -ForegroundColor Cyan

switch ($hostingType) {
    'storage' {
        $storageAccount = Require-EnvValue -Path $EnvFile -Name "STORAGE_ACCOUNT_$envSuffix"
        Write-Host "  Storage account: $storageAccount" -ForegroundColor DarkGray

        # az storage blob upload-batch uploads every file under
        # $publishDir into the $web container. AAD auth — requires the
        # operator to hold "Storage Blob Data Contributor" on the account.
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

        # SPA fallback: deep links (e.g. /authentication/login-callback)
        # must serve index.html instead of 404. Written here, AFTER
        # publish, so it does NOT land in service-worker-assets.js — SWA
        # reserves staticwebapp.config.json and returns 404 for it, which
        # would otherwise fail the offline SW install.
        $swaConfig = Join-Path $publishDir 'staticwebapp.config.json'
        Set-Content -LiteralPath $swaConfig -NoNewline -Value @'
{
  "navigationFallback": {
    "rewrite": "/index.html"
  }
}
'@
        Write-Host "  Wrote staticwebapp.config.json (SPA fallback)" -ForegroundColor DarkGray

        # Fetch a one-time deployment token for this SWA. Stored in a
        # variable rather than on the command line so it does not show
        # up in PowerShell's history.
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
        # `az webapp deploy --type zip`.
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

# ------------------------------------------------------------------
# Optional: purge Front Door cache
# ------------------------------------------------------------------
# AFD profiles typically live in a shared networking/DNS resource
# group that is different from the storage RG, so the purge needs its
# own FRONT_DOOR_RG_<ENV>. Falls back to the storage RG only when
# FRONT_DOOR_RG_* is not set. Leave FRONT_DOOR_* blank to skip
# entirely (e.g. on Static Web Apps, which is not behind AFD).
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

# ------------------------------------------------------------------
# Push the auto-bump commit
# ------------------------------------------------------------------
# The chore(version) commit from the auto-bump step is still local —
# push it now that the upload and cache purge have succeeded so the
# committed version on origin always matches what's actually deployed.
if ($bumpedThisDeploy) {
    $currentBranch = (git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null).Trim()
    Write-Host ""
    Write-Host "Pushing auto-bump commit to origin/$currentBranch..." -ForegroundColor Cyan
    git -C $repoRoot push origin $currentBranch 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "git push failed; the auto-bump commit is local. Resolve and push manually so origin matches the deployed version."
    }
}

Write-Host ""
Write-Host "Deployment to $Environment complete." -ForegroundColor Green
