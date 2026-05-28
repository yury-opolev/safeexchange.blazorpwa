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

Write-Host ""
Write-Host "Admin panel deploy to $envSuffix complete." -ForegroundColor Green
