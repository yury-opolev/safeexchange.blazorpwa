#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy SafeExchange.AdminPanel (Blazor WASM) to its dedicated
    storage-account static-site hosting.

.DESCRIPTION
    Mirror of deploy-pwa.ps1 for the admin-panel sibling project. The
    panel is a standalone Blazor WASM site (separate Entra UX, separate
    visual language) and is hosted on its own storage account so its
    blast radius is independent of the main PWA.

    The script:
      1. Reads STORAGE_ACCOUNT_ADMIN_<ENV> + APPSETTINGS_*_<ENV> from
         deployment/.env (gitignored). The APPSETTINGS_* values are
         shared with deploy-pwa.ps1 — both sites authenticate against
         the same Entra app registration and call the same backend.
      2. Publishes Release to a pinned bin/Release/admin-publish folder.
      3. Rewrites the published appsettings.json with env-specific
         tenant/clientId/backend/scope (same shape as the PWA — no
         service-worker SRI patching because the admin panel has no
         service worker).
      4. Uploads the wwwroot to the `$web` container of the storage
         account named in STORAGE_ACCOUNT_ADMIN_<ENV>.
      5. Skips the Front Door cache purge for now — the admin panel is
         not behind AFD yet. Wire FRONT_DOOR_*_ADMIN_<ENV> later when
         we add a route.

    First-time setup: the storage account must already exist with
    static-website hosting enabled. Provision it once with:

        az storage account create --name <accountName> \
            --resource-group safeexchange-staging-web \
            --location northeurope --sku Standard_LRS --kind StorageV2
        az storage blob service-properties update \
            --account-name <accountName> --static-website \
            --index-document index.html --404-document index.html \
            --auth-mode login

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

    Publishes Release and uploads to the configured test target.
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

$envSuffix     = $Environment.ToUpperInvariant()
$storageAccount = Require-EnvValue -Path $EnvFile -Name "STORAGE_ACCOUNT_ADMIN_$envSuffix"
$subscription   = Read-EnvValue -Path $EnvFile -Name 'SUBSCRIPTION'

Write-Host "Environment:    $Environment" -ForegroundColor DarkGray
Write-Host "Project:        SafeExchange.AdminPanel" -ForegroundColor DarkGray
Write-Host "Storage:        $storageAccount" -ForegroundColor DarkGray
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

# ──────────────────────────────────────────────────────────────────
# Auto-bump version.json patch number
# ──────────────────────────────────────────────────────────────────
# Mirrors the PWA flow: every deploy = a new build, so bump the patch
# number in SafeExchange.AdminPanel/wwwroot/version.json before publishing.
# Skipped when HEAD already touched the version field (avoids double-
# bumping when staging + prod are deployed back-to-back for the same
# code) and under -WhatIf.
$bumpedThisDeploy = $false
$bumpRepoRoot     = Split-Path $PSScriptRoot -Parent
$srcVersionJson   = Join-Path $bumpRepoRoot 'SafeExchange.AdminPanel\wwwroot\version.json'
if ((-not $WhatIf) -and (Test-Path $srcVersionJson))
{
    $relPath = 'SafeExchange.AdminPanel/wwwroot/version.json'
    $headDiff = git -C $bumpRepoRoot show HEAD --format='' -- $relPath 2>&1
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

            git -C $bumpRepoRoot add $relPath | Out-Null
            git -C $bumpRepoRoot commit -m "chore(adminpanel-version): auto-bump $oldVersion -> $($info.version) before $Environment deploy" -q
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

# Build.
Write-Host ""
Write-Host "Publishing $adminProject (Release)..." -ForegroundColor Cyan

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

# Rewrite published appsettings.json with env-specific values. Shape is
# identical to the main PWA — both call the same backend and use the
# same Entra app registration.
$appsettingsAuthority = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_AUTHORITY_$envSuffix"
$appsettingsClientId  = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_CLIENT_ID_$envSuffix"
$appsettingsBackend   = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_BACKEND_$envSuffix"
$appsettingsScope     = Require-EnvValue -Path $EnvFile -Name "APPSETTINGS_SCOPE_$envSuffix"

$publishAppsettings = Join-Path $publishDir 'appsettings.json'
if (-not (Test-Path $publishAppsettings))
{
    throw "Published appsettings.json not found at $publishAppsettings"
}

Write-Host "Rewriting $publishAppsettings with $envSuffix overrides..." -ForegroundColor DarkGray
$settings = Get-Content -LiteralPath $publishAppsettings -Raw | ConvertFrom-Json
$settings.AzureAdB2C.Authority   = $appsettingsAuthority
$settings.AzureAdB2C.ClientId    = $appsettingsClientId
$settings.BackendApi.BaseAddress = $appsettingsBackend
$settings.AccessTokenScopes      = @($appsettingsScope)
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $publishAppsettings -NoNewline
Write-Host "  ClientId:    $appsettingsClientId" -ForegroundColor DarkGray
Write-Host "  Authority:   $appsettingsAuthority" -ForegroundColor DarkGray
Write-Host "  BackendApi:  $appsettingsBackend" -ForegroundColor DarkGray
Write-Host "  Scope:       $appsettingsScope" -ForegroundColor DarkGray

# Stamp the build date into the published version.json so the panel
# can show "vX.Y.Z (yyyy-MM-dd)" without us having to remember to
# touch the file by hand. No SW SRI patching here — admin panel has
# no service worker, unlike the main PWA.
$publishVersionJson = Join-Path $publishDir 'version.json'
if (Test-Path $publishVersionJson)
{
    $versionInfo = Get-Content -LiteralPath $publishVersionJson -Raw | ConvertFrom-Json
    $versionInfo.buildDate = [System.DateTime]::UtcNow.ToString('yyyy-MM-dd')
    $versionInfo | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $publishVersionJson -NoNewline
    Write-Host "Stamped version.json: v$($versionInfo.version) ($($versionInfo.buildDate))" -ForegroundColor DarkGray
}
else
{
    Write-Warning "Published version.json not found at $publishVersionJson — version display will be empty."
}

if ($WhatIf)
{
    Write-Host ""
    Write-Host "What-if: skipping upload. Nothing was uploaded." -ForegroundColor Yellow
    exit 0
}

# Upload.
Write-Host ""
Write-Host "Uploading to storage..." -ForegroundColor Cyan

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

# Optional AFD purge — only if FRONT_DOOR_*_ADMIN_<ENV> are set. Admin
# panel may not be behind AFD yet, so this stays a no-op by default.
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
    Write-Host "FRONT_DOOR_*_ADMIN_$envSuffix not set — skipping AFD purge (admin panel not behind AFD yet)." -ForegroundColor DarkGray
}

# Push the auto-bump commit. Kept local until the upload succeeded so
# origin never carries a version that didn't actually deploy.
if ($bumpedThisDeploy)
{
    $currentBranch = (git -C $bumpRepoRoot rev-parse --abbrev-ref HEAD 2>$null).Trim()
    Write-Host ""
    Write-Host "Pushing auto-bump commit to origin/$currentBranch..." -ForegroundColor Cyan
    git -C $bumpRepoRoot push origin $currentBranch 2>&1 | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        Write-Warning "git push failed; the auto-bump commit is local. Resolve and push manually so origin matches the deployed version."
    }
}

Write-Host ""
Write-Host "Admin panel deploy to $envSuffix complete." -ForegroundColor Green
