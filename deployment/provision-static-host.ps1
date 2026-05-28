#!/usr/bin/env pwsh
<#
.SYNOPSIS
    First-time provisioning of a storage account for static-site
    hosting (the SafeExchange PWA or the SafeExchange.AdminPanel).
    Idempotent — safe to re-run.

.DESCRIPTION
    Creates a Standard_LRS / StorageV2 account in the specified
    resource group and enables the static-website endpoint (`$web`
    container, index.html, 404.html). After this runs once per
    environment, deploy-pwa.ps1 / deploy-adminpanel.ps1 can upload
    on every change.

    Storage-account names must be 3-24 chars, lowercase letters +
    digits only, globally unique across all of Azure.

.PARAMETER AccountName
    Storage account name to provision. Example: 'safeexchangestagingweb'
    for the PWA, 'saexstagingadm' for the admin panel.

.PARAMETER ResourceGroup
    Azure resource group that owns the account. Created if missing.

.PARAMETER Location
    Azure region. Default 'northeurope' to match the rest of the
    SafeExchange stack.

.EXAMPLE
    ./deployment/provision-static-host.ps1 -AccountName saexstagingadm \
        -ResourceGroup safeexchange-staging-web

    Provisions the admin-panel static-site host for staging.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9]{3,24}$')]
    [string]$AccountName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter()]
    [string]$Location = 'northeurope'
)

$env:MSYS_NO_PATHCONV = '1'
$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

try
{
    $null = & az --version 2>&1
}
catch
{
    throw "Azure CLI ('az') is not installed or not on PATH."
}

$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account)
{
    throw "az is not logged in. Run 'az login' and try again."
}

Write-Host "Subscription:    $($account.name) ($($account.id))" -ForegroundColor DarkGray
Write-Host "Resource group:  $ResourceGroup" -ForegroundColor DarkGray
Write-Host "Account:         $AccountName" -ForegroundColor DarkGray
Write-Host "Location:        $Location" -ForegroundColor DarkGray

# RG (idempotent — create if missing).
$rgExists = & az group exists --name $ResourceGroup
if ($rgExists -ne 'true')
{
    Write-Host ""
    Write-Host "Creating resource group $ResourceGroup..." -ForegroundColor Cyan
    & az group create --name $ResourceGroup --location $Location | Out-Null
    if ($LASTEXITCODE -ne 0)
    {
        throw "Failed to create resource group $ResourceGroup."
    }
}

# Storage account (idempotent — re-run is a no-op if account already exists).
$existing = az storage account show --name $AccountName --resource-group $ResourceGroup --output json 2>$null
if (-not $existing)
{
    Write-Host ""
    Write-Host "Creating storage account $AccountName..." -ForegroundColor Cyan
    & az storage account create `
        --name $AccountName `
        --resource-group $ResourceGroup `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --allow-blob-public-access true | Out-Null
    if ($LASTEXITCODE -ne 0)
    {
        throw "Failed to create storage account $AccountName."
    }
}
else
{
    Write-Host "Storage account $AccountName already exists — skipping create." -ForegroundColor DarkGray
}

# Static-website endpoint (idempotent — sets the property even when
# already enabled, which is a no-op).
Write-Host ""
Write-Host "Enabling static website on $AccountName..." -ForegroundColor Cyan
& az storage blob service-properties update `
    --account-name $AccountName `
    --static-website `
    --index-document index.html `
    --404-document index.html `
    --auth-mode login | Out-Null
if ($LASTEXITCODE -ne 0)
{
    throw "Failed to enable static website on $AccountName."
}

$webUrl = az storage account show --name $AccountName --query primaryEndpoints.web -o tsv
Write-Host ""
Write-Host "Static-site URL: $webUrl" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Set STORAGE_ACCOUNT_<ENV> (or STORAGE_ACCOUNT_ADMIN_<ENV>) in deployment/.env to '$AccountName'."
Write-Host "  2. Grant yourself 'Storage Blob Data Contributor' on the account if not already (deploy uses --auth-mode login)."
Write-Host "  3. Run deploy-pwa.ps1 or deploy-adminpanel.ps1 to publish."
