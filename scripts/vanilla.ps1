#Requires -Version 5.1
<#
.SYNOPSIS
    Removes mod and BepInEx to revert to vanilla Valheim.
.DESCRIPTION
    Uses cameraunlock-core shared modules.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Import shared modules
$coreModulesPath = Join-Path $PSScriptRoot "..\cameraunlock-core\powershell"
Import-Module (Join-Path $coreModulesPath "GamePathDetection.psm1") -Force
Import-Module (Join-Path $coreModulesPath "ModLoaderSetup.psm1") -Force

$GameId = 'Valheim'

Write-Host ""
Write-Host "=== Valheim - Revert to Vanilla ===" -ForegroundColor Magenta
Write-Host ""

# Find game
$gamePath = Find-GamePath -GameId $GameId
if (-not $gamePath) {
    Write-Host "ERROR: Valheim installation not found" -ForegroundColor Red
    exit 1
}

Write-Host "Found Valheim at: $gamePath" -ForegroundColor Green
Write-Host ""

# Check state file
$state = Get-ModLoaderState -GamePath $gamePath
$installedByUs = $false
if ($state -and $state.framework -and $state.framework.installed_by_us) {
    $installedByUs = $true
}

# Remove BepInEx if we installed it
if ($installedByUs) {
    Write-Host "Removing BepInEx (installed by us)..." -ForegroundColor Yellow

    $filesToRemove = @(
        "winhttp.dll",
        "doorstop_config.ini",
        ".doorstop_version"
    )

    foreach ($file in $filesToRemove) {
        $path = Join-Path $gamePath $file
        if (Test-Path $path) {
            Remove-Item $path -Force
            Write-Host "  Removed: $file" -ForegroundColor Gray
        }
    }

    $bepinexDir = Join-Path $gamePath "BepInEx"
    if (Test-Path $bepinexDir) {
        Remove-Item $bepinexDir -Recurse -Force
        Write-Host "  Removed: BepInEx/" -ForegroundColor Gray
    }
} else {
    Write-Host "BepInEx was not installed by us - removing only mod files" -ForegroundColor Yellow

    $pluginsDir = Get-BepInExPluginsPath -GamePath $gamePath
    $modDir = Join-Path $pluginsDir "ValheimHeadTracking"
    if (Test-Path $modDir) {
        Remove-Item $modDir -Recurse -Force
        Write-Host "  Removed: BepInEx/plugins/ValheimHeadTracking/" -ForegroundColor Gray
    }
}

# Remove state file
$stateFile = Join-Path $gamePath ".headtracking-state.json"
if (Test-Path $stateFile) {
    Remove-Item $stateFile -Force
    Write-Host "  Removed: .headtracking-state.json" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Valheim reverted to vanilla state." -ForegroundColor Green
Write-Host ""
