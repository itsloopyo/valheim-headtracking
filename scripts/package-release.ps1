#!/usr/bin/env pwsh
#Requires -Version 5.1
# Custom packaging for Valheim Head Tracking (BepInEx mod)
# Produces two ZIPs:
#   - ValheimHeadTracking-v{version}-installer.zip (GitHub Release: install.cmd + plugins/ + vendor/ + shared/ + docs)
#   - ValheimHeadTracking-v{version}-nexus.zip     (Nexus Mods: extract-to-game-folder layout)
#
# Consumes whatever is committed under vendor/. Refreshing vendored
# loaders is a manual dev action via `pixi run update-deps`. This script
# never touches the network. See AGENTS.md "Vendoring Third-Party
# Dependencies".

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = 'SilentlyContinue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

# Get version from .csproj
Import-Module (Join-Path $projectDir "cameraunlock-core/powershell/ReleaseWorkflow.psm1") -Force
$version = Get-CsprojVersion -CsprojPath (Join-Path $projectDir "src/ValheimHeadTracking/ValheimHeadTracking.csproj")

Write-Host "=== Valheim Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

$releaseDir = Join-Path $projectDir "release"
$buildOutputDir = Join-Path $projectDir "src/ValheimHeadTracking/bin/Release/net48"
$scriptsDir = Join-Path $projectDir "scripts"
$vendorBepDir = Join-Path $projectDir "vendor/bepinex"
$coreRoot = Join-Path $projectDir "cameraunlock-core"

# Create release directory
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Validate required source files upfront (fail fast)
$modDlls = @(
    "ValheimHeadTracking.dll",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll",
    "CameraUnlock.Core.Unity.BepInEx.dll"
)

foreach ($dll in $modDlls) {
    $dllPath = Join-Path $buildOutputDir $dll
    if (-not (Test-Path $dllPath)) {
        throw "Required DLL not found: $dllPath. Run 'pixi run build' first."
    }
}

foreach ($script in @("install.cmd", "uninstall.cmd")) {
    $scriptPath = Join-Path $scriptsDir $script
    if (-not (Test-Path $scriptPath)) {
        throw "Required script not found: $scriptPath"
    }
}

# Validate vendored loader is committed. Refresh is a manual dev action
# (`pixi run update-deps`); this script never hits the network.
$vendorBepZip = Join-Path $vendorBepDir "BepInEx_win_x64.zip"
if (-not (Test-Path $vendorBepZip)) {
    throw "Bundled BepInEx missing: $vendorBepZip. Run 'pixi run update-deps' and commit the result."
}
foreach ($vendorFile in @("LICENSE", "README.md")) {
    $vp = Join-Path $vendorBepDir $vendorFile
    if (-not (Test-Path $vp)) {
        throw "Required vendor file missing: $vp. Run 'pixi run update-deps' and commit the result."
    }
}

# --- GitHub Release ZIP (with installer) ---

Write-Host "--- GitHub Release ZIP ---" -ForegroundColor Yellow
Write-Host ""

$ghStagingDir = Join-Path $releaseDir "staging-github"
if (Test-Path $ghStagingDir) { Remove-Item -Recurse -Force $ghStagingDir }
New-Item -ItemType Directory -Path $ghStagingDir -Force | Out-Null

# Copy install/uninstall scripts
foreach ($script in @("install.cmd", "uninstall.cmd")) {
    Copy-Item (Join-Path $scriptsDir $script) -Destination $ghStagingDir -Force
    Write-Host "  $script" -ForegroundColor Green
}

# Copy mod DLLs to plugins subfolder
$pluginsDir = Join-Path $ghStagingDir "plugins"
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $pluginsDir -Force
    Write-Host "  plugins/$dll" -ForegroundColor Green
}

# Bundle vendored BepInEx (LGPL-2.1, see THIRD-PARTY-NOTICES.md) as fallback.
$ghVendorDir = Join-Path $ghStagingDir "vendor/bepinex"
New-Item -ItemType Directory -Path $ghVendorDir -Force | Out-Null
foreach ($vendorFile in @("BepInEx_win_x64.zip", "LICENSE", "README.md")) {
    Copy-Item (Join-Path $vendorBepDir $vendorFile) -Destination $ghVendorDir -Force
    Write-Host "  vendor/bepinex/$vendorFile" -ForegroundColor Green
}

# Bundle the shared detection bundle for install.cmd's shim. Prefer the
# helper from ReleaseWorkflow.psm1; fall back to an inline copy if the
# submodule predates the helper.
if (Get-Command Copy-SharedBundle -ErrorAction SilentlyContinue) {
    Copy-SharedBundle -StagingDir $ghStagingDir -CoreRoot $coreRoot
} else {
    $sharedSources = @(
        @{ Src = 'data/games.json';                   Dest = 'games.json' }
        @{ Src = 'powershell/GamePathDetection.psm1'; Dest = 'GamePathDetection.psm1' }
        @{ Src = 'scripts/find-game.ps1';             Dest = 'find-game.ps1' }
    )
    $missingShared = @()
    foreach ($s in $sharedSources) {
        if (-not (Test-Path (Join-Path $coreRoot $s.Src))) {
            $missingShared += $s.Src
        }
    }
    if ($missingShared.Count -gt 0) {
        Write-Host "  WARNING: cameraunlock-core is missing shared-bundle files:" -ForegroundColor Yellow
        foreach ($m in $missingShared) { Write-Host "    - $m" -ForegroundColor Yellow }
        Write-Host "  Run 'pixi run sync' to bump the submodule. Skipping shared/ bundle; install.cmd will rely on the dev-tree fallback." -ForegroundColor Yellow
    } else {
        $sharedDir = Join-Path $ghStagingDir "shared"
        New-Item -ItemType Directory -Path $sharedDir -Force | Out-Null
        foreach ($s in $sharedSources) {
            Copy-Item (Join-Path $coreRoot $s.Src) -Destination (Join-Path $sharedDir $s.Dest) -Force
            Write-Host "  shared/$($s.Dest)" -ForegroundColor Green
        }
    }
}

# Copy documentation
$docFiles = @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectDir $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $ghStagingDir -Force
        Write-Host "  $doc" -ForegroundColor Green
    }
}

$ghZipName = "ValheimHeadTracking-v$version-installer.zip"
$ghZipPath = Join-Path $releaseDir $ghZipName
if (Test-Path $ghZipPath) { Remove-Item $ghZipPath -Force }

Write-Host ""
Write-Host "Creating GitHub ZIP..." -ForegroundColor Cyan

Push-Location $ghStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $ghZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $ghStagingDir

$ghZipSize = (Get-Item $ghZipPath).Length / 1KB
Write-Host ("  $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green

# --- Nexus Mods ZIP (extract-to-game-folder) ---

Write-Host ""
Write-Host "--- Nexus Mods ZIP ---" -ForegroundColor Yellow
Write-Host ""

$nexusStagingDir = Join-Path $releaseDir "staging-nexus"
if (Test-Path $nexusStagingDir) { Remove-Item -Recurse -Force $nexusStagingDir }

# Mirror game directory structure: BepInEx/plugins/ValheimHeadTracking/
$nexusPluginDir = Join-Path $nexusStagingDir "BepInEx\plugins\ValheimHeadTracking"
New-Item -ItemType Directory -Path $nexusPluginDir -Force | Out-Null

foreach ($dll in $modDlls) {
    Copy-Item (Join-Path $buildOutputDir $dll) -Destination $nexusPluginDir -Force
    Write-Host "  BepInEx/plugins/ValheimHeadTracking/$dll" -ForegroundColor Green
}

$nexusZipName = "ValheimHeadTracking-v$version-nexus.zip"
$nexusZipPath = Join-Path $releaseDir $nexusZipName
if (Test-Path $nexusZipPath) { Remove-Item $nexusZipPath -Force }

Write-Host ""
Write-Host "Creating Nexus ZIP..." -ForegroundColor Cyan

Push-Location $nexusStagingDir
try {
    Compress-Archive -Path ".\*" -DestinationPath $nexusZipPath -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force $nexusStagingDir

$nexusZipSize = (Get-Item $nexusZipPath).Length / 1KB
Write-Host ("  $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# --- Summary ---

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host ("GitHub Release: $ghZipPath ({0:N1} KB)" -f $ghZipSize) -ForegroundColor Green
Write-Host ("Nexus Mods:     $nexusZipPath ({0:N1} KB)" -f $nexusZipSize) -ForegroundColor Green

# Output both zip paths for CI capture (one per line)
Write-Output $ghZipPath
Write-Output $nexusZipPath
