#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy SafeExchange.AdminPanel (Blazor WASM) to the Azure hosting
    target configured in deployment/.env.

.DESCRIPTION
    Mirror of deploy-pwa.ps1 for the admin-panel sibling project. The
    panel is a standalone Blazor WASM site (separate Entra UX, separate
    visual language) hosted independently of the main PWA.

    Hosting target branches on HOSTING_TYPE_ADMIN_<ENV> (defaults to
    'storage' when unset, for back-compat):

      storage       -> az storage blob upload-batch into the $web
                      container of STORAGE_ACCOUNT_ADMIN_<ENV>
      staticwebapp  -> swa deploy to STATIC_WEB_APP_ADMIN_<ENV>

    The APPSETTINGS_*_<ENV> values are shared with deploy-pwa.ps1 — both
    sites authenticate against the same Entra app registration and call
    the same backend. They are injected into the SOURCE appsettings.json
    BEFORE `dotnet publish` (so the precompressed appsettings.json.gz/.br
    are derived from the correct content), then the source tree is
    restored. The admin panel has no service worker, so there is no SW
    integrity patching.

.PARAMETER Environment
    Which environment to deploy. 'test' maps to *_TEST variables,
    'prd' to *_PRD variables.

.PARAMETER WhatIf
    Build and stage the output but skip the upload.

.PARAMETER EnvFile
    Override the path to the .env file. Defaults to
    deployment/.env alongside this script.

.EXAMPLE
    ./deployment/deploy-adminpanel.ps1 -Environment test
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'prd')]
    [string]$Environment,

    [Parameter()]
    [switch]$WhatIf,

    [Parameter()]
    [string]$EnvFile
)

# Disable Git Bash MSYS path translation for az CLI calls (same reason
# as deploy-pwa.ps1 — leading '/' on Azure resource IDs would otherwise
# get rewritten to 'C:/Program Files/Git/' under bash on Windows).
$env:MSYS_NO_PATHCONV = '1'

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

$here       = $PSScriptRoot
$repoRoot   = Split-Path -Parent $here
$adminProject = Join-Path $repoRoot 'SafeExchange.AdminPanel/SafeExchange.AdminPanel.csproj'
$publishRoot  = Join-Path $repoRoot 'SafeExchange.AdminPanel/bin/Release/admin-publish'
$publishDir   = Join-Path $publishRoot 'wwwroot'

# Source files mutated in place before publish and restored afterwards.
$srcAppsettings = Join-Path $repoRoot 'SafeExchange.AdminPanel/wwwroot/appsettings.json'
$srcVersionJson = Join-Path $repoRoot 'SafeExchange.AdminPanel/wwwroot/version.json'

if (-not $EnvFile)
{
    $EnvFile = Join-Path $here '.env'
}

if (-not (Test-Path $adminProject))
{
    throw "AdminPanel project file not found: $adminProject"
}

# .env reader (verbatim from deploy-pwa.ps1 — kept independent so the
# two scripts can evolve without coupling).
function Read-EnvValue
{
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path $Path))
    {
        return ''
    }

    $pattern = '^' + [regex]::Escape($Name) + '\s*=\s*(.*)$'
    foreach ($line in Get-Content -LiteralPath $Path)
    {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#'))
        {
            continue
        }

        if ($trimmed -match $pattern)
        {
            return $matches[1].Trim().Trim('"').Trim("'")
        }
    }

    return ''
}

function Require-EnvValue
{
    param(
        [string]$Path,
        [string]$Name
    )

    $value = Read-EnvValue -Path $Path -Name $Name
    if (-not $value)
    {
        throw "Required variable '$Name' is not set in $Path. See deployment/.env.example for the full list."
    }

    return $value
}

$envSuffix    = $Environment.ToUpperInvariant()
$subscription = Read-EnvValue -Path $EnvFile -Name 'SUBSCRIPTION'

# Hosting type for the admin panel. Defaults to 'storage' so existing
# .env files (which only set STORAGE_ACCOUNT_ADMIN_*) keep working.
$hostingType = (Read-EnvValue -Path $EnvFile -Name "HOSTING_TYPE_ADMIN_$envSuffix").ToLowerInvariant()
if (-not $hostingType) { $hostingType = 'storage' }

$validTypes = @('storage', 'staticwebapp')
if ($hostingType -notin $validTypes)
{
    throw "HOSTING_TYPE_ADMIN_$envSuffix must be one of: $($validTypes -join ', '). Got '$hostingType'."
}

Write-Host "Environment:    $Environment" -ForegroundColor DarkGray
Write-Host "Project:        SafeExchange.AdminPanel" -ForegroundColor DarkGray
Write-Host "Hosting type:   $hostingType" -ForegroundColor DarkGray
if ($subscription)
{
    Write-Host "Subscription:   $subscription" -ForegroundColor DarkGray
}

# Sanity-check az login.
try
{
    $null = & az --version 2>&1
}
catch
{
    throw "Azure CLI ('az') is not installed or not on PATH. Install it from https://aka.ms/azcliinstall and try again."
}

$accountShow = az account show --output json 2>$null | ConvertFrom-Json
if (-not $accountShow)
{
    throw "az is not logged in. Run 'az login' and try again."
}

if ($subscription -and $accountShow.id -ne $subscription)
{
    Write-Host "Setting active subscription to $subscription" -ForegroundColor DarkGray
    & az account set --subscription $subscription | Out-Null
}

# ------------------------------------------------------------------
# Auto-bump version.json patch number
# ------------------------------------------------------------------
# Mirrors the PWA flow. Skipped when HEAD already touched the version
# field and under -WhatIf.
$bumpedThisDeploy = $false
if ((-not $WhatIf) -and (Test-Path $srcVersionJson))
{
    $relPath = 'SafeExchange.AdminPanel/wwwroot/version.json'
    $headDiff = git -C $repoRoot show HEAD --format='' -- $relPath 2>&1
    $versionAlreadyChanged = @($headDiff | Where-Object { $_ -match '^\+\s*"version"\s*:' }).Count -gt 0

    if ($versionAlreadyChanged)
    {
        $current = (Get-Content -LiteralPath $srcVersionJson -Raw | ConvertFrom-Json).version
        Write-Host ""
        Write-Host "Version: $current (HEAD already bumped this; no auto-bump for this deploy)" -ForegroundColor DarkGray
    }
    else
    {
        $info  = Get-Content -LiteralPath $srcVersionJson -Raw | ConvertFrom-Json
        $parts = $info.version -split '\.'
        if ($parts.Length -lt 3)
        {
            Write-Warning "version.json version '$($info.version)' is not MAJOR.MINOR.PATCH; skipping auto-bump."
        }
        else
        {
            $oldVersion = $info.version
            $parts[2]   = [string]([int]$parts[2] + 1)
            $info.version = $parts -join '.'
            $info | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $srcVersionJson -NoNewline

            git -C $repoRoot add $relPath | Out-Null
            git -C $repoRoot commit -m "chore(adminpanel-version): auto-bump $oldVersion -> $($info.version) before $Environment deploy" -q
            if ($LASTEXITCODE -eq 0)
            {
                $bumpedThisDeploy = $true
                Write-Host ""
                Write-Host "Auto-bumped admin-panel version: $oldVersion -> $($info.version) (committed locally; pushed after successful deploy)" -ForegroundColor Cyan
            }
            else
            {
                Write-Warning "Auto-bump commit failed (exit $LASTEXITCODE). The bumped version is on disk; resolve manually."
            }
        }
    }
}

# ------------------------------------------------------------------
# Inject env-specific appsettings.json values BEFORE publish
# ------------------------------------------------------------------
# Same rationale as deploy-pwa.ps1: write the real per-env values into
# the source appsettings.json before publish so the precompressed
# .gz/.br twins are derived from the correct content. Build date is
# stamped into version.json the same way. Both source files are backed
# up and restored in the finally below.
$appsettingsAuthority = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_AUTHORITY_$envSuffix"
$appsettingsClientId  = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_CLIENT_ID_$envSuffix"
$appsettingsBackend   = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_BACKEND_$envSuffix"
$appsettingsScope     = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_SCOPE_$envSuffix"

if (-not (Test-Path $srcAppsettings))
{
    throw "Source appsettings.json not found: $srcAppsettings"
}

# Back up OUTSIDE the project tree (temp dir). A backup next to the source in
# wwwroot exists during `dotnet publish` and can be swept into the published
# output as a static web asset — and then SW-cached — shipping the
# pre-injection appsettings.json. A temp path is never a publish input.
$appsettingsBackup = Join-Path ([System.IO.Path]::GetTempPath()) ("appsettings.json.deploy-bak." + [guid]::NewGuid())
$versionBackup     = if (Test-Path $srcVersionJson) { Join-Path ([System.IO.Path]::GetTempPath()) ("version.json.deploy-bak." + [guid]::NewGuid()) } else { $null }
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
Write-Host "  ClientId:    $appsettingsClientId" -ForegroundColor DarkGray
Write-Host "  Authority:   $appsettingsAuthority" -ForegroundColor DarkGray
Write-Host "  BackendApi:  $appsettingsBackend" -ForegroundColor DarkGray
Write-Host "  Scope:       $appsettingsScope" -ForegroundColor DarkGray

if (Test-Path $srcVersionJson)
{
    $versionInfo = Get-Content -LiteralPath $srcVersionJson -Raw | ConvertFrom-Json
    $versionInfo.buildDate = [System.DateTime]::UtcNow.ToString('yyyy-MM-dd')
    $versionInfo | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $srcVersionJson -NoNewline
    Write-Host "  version:     v$($versionInfo.version) ($($versionInfo.buildDate))" -ForegroundColor DarkGray
}

# ------------------------------------------------------------------
# Build and publish (source restored in finally)
# ------------------------------------------------------------------
Write-Host ""
Write-Host "Publishing $adminProject (Release)..." -ForegroundColor Cyan
try
{
    if (Test-Path $publishRoot)
    {
        Remove-Item -LiteralPath $publishRoot -Recurse -Force
    }

    & dotnet publish $adminProject -c Release -o $publishRoot
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path $publishDir))
    {
        throw "Publish output not found at expected location: $publishDir"
    }

    Write-Host "Publish output: $publishDir" -ForegroundColor DarkGray
}
finally
{
    Copy-Item -LiteralPath $appsettingsBackup -Destination $srcAppsettings -Force
    Remove-Item -LiteralPath $appsettingsBackup -Force
    if ($versionBackup)
    {
        Copy-Item -LiteralPath $versionBackup -Destination $srcVersionJson -Force
        Remove-Item -LiteralPath $versionBackup -Force
    }
    Write-Host "Restored source appsettings.json / version.json." -ForegroundColor DarkGray
}

if ($WhatIf)
{
    Write-Host ""
    Write-Host "What-if: skipping upload. Nothing was uploaded." -ForegroundColor Yellow
    exit 0
}

# ------------------------------------------------------------------
# Upload — branches on HOSTING_TYPE_ADMIN
# ------------------------------------------------------------------
Write-Host ""
Write-Host "Uploading to $hostingType target..." -ForegroundColor Cyan

switch ($hostingType)
{
    'storage'
    {
        $storageAccount = Require-EnvValue -Path $EnvFile -Name "STORAGE_ACCOUNT_ADMIN_$envSuffix"
        Write-Host "  Storage account: $storageAccount" -ForegroundColor DarkGray

        $azArgs = @(
            'storage', 'blob', 'upload-batch'
            '--account-name',   $storageAccount
            '--destination',    '$web'
            '--source',         $publishDir
            '--overwrite',      'true'
            '--auth-mode',      'login'
        )
        & az @azArgs
        if ($LASTEXITCODE -ne 0)
        {
            throw "Storage upload failed with exit code $LASTEXITCODE"
        }
    }

    'staticwebapp'
    {
        $staticWebApp  = Require-EnvValue -Path $EnvFile -Name "STATIC_WEB_APP_ADMIN_$envSuffix"
        $resourceGroup = Require-EnvValue -Path $EnvFile -Name "RESOURCE_GROUP_$envSuffix"
        Write-Host "  Static Web App: $staticWebApp" -ForegroundColor DarkGray

        # SPA fallback so deep links serve index.html instead of 404.
        # Written post-publish (admin has no SW, but keep it parallel
        # with the PWA and out of any future asset manifest).
        $swaConfig = Join-Path $publishDir 'staticwebapp.config.json'
        Set-Content -LiteralPath $swaConfig -NoNewline -Value @'
{
  "navigationFallback": {
    "rewrite": "/index.html"
  }
}
'@
        Write-Host "  Wrote staticwebapp.config.json (SPA fallback)" -ForegroundColor DarkGray

        $deployToken = az staticwebapp secrets list `
            --name $staticWebApp `
            --resource-group $resourceGroup `
            --query "properties.apiKey" -o tsv
        if (-not $deployToken)
        {
            throw "Failed to fetch deployment token for SWA '$staticWebApp'."
        }

        try
        {
            $null = & swa --version 2>&1
        }
        catch
        {
            throw "Static Web Apps CLI ('swa') is not installed. Run 'npm install -g @azure/static-web-apps-cli' and try again."
        }

        & swa deploy $publishDir --deployment-token $deployToken --env production
        if ($LASTEXITCODE -ne 0)
        {
            throw "swa deploy failed with exit code $LASTEXITCODE"
        }
    }
}

# Optional AFD purge — only if FRONT_DOOR_*_ADMIN_<ENV> are set. Admin
# panel may not be behind AFD, so this stays a no-op by default.
$afdProfile  = Read-EnvValue -Path $EnvFile -Name "FRONT_DOOR_PROFILE_ADMIN_$envSuffix"
$afdEndpoint = Read-EnvValue -Path $EnvFile -Name "FRONT_DOOR_ENDPOINT_ADMIN_$envSuffix"
$afdRg       = Read-EnvValue -Path $EnvFile -Name "FRONT_DOOR_RG_ADMIN_$envSuffix"

if ($afdProfile -and $afdEndpoint -and $afdRg)
{
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
    if ($LASTEXITCODE -ne 0)
    {
        Write-Warning "Front Door purge failed with exit code $LASTEXITCODE. The upload succeeded; clients may see cached content until TTL expires."
    }
}
else
{
    Write-Host "FRONT_DOOR_*_ADMIN_$envSuffix not set — skipping AFD purge." -ForegroundColor DarkGray
}

# Push the auto-bump commit now that the upload succeeded.
if ($bumpedThisDeploy)
{
    $currentBranch = (git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null).Trim()
    Write-Host ""
    Write-Host "Pushing auto-bump commit to origin/$currentBranch..." -ForegroundColor Cyan
    git -C $repoRoot push origin $currentBranch 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        Write-Warning "git push failed; the auto-bump commit is local. Resolve and push manually so origin matches the deployed version."
    }
}

Write-Host ""
Write-Host "Admin panel deploy to $envSuffix complete." -ForegroundColor Green
