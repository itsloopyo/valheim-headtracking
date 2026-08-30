#!/usr/bin/env pwsh
# Populates src/ValheimHeadTracking/libs/ for a game-free build, from REPO
# FILES ONLY - no Valheim install required - so `pixi run package` builds
# identically locally and in CI:
#   - BepInEx.dll / 0Harmony.dll : extracted from the vendored BepInEx zip
#   - UnityEngine*.dll           : compiled by the shared stub builder in
#                                  cameraunlock-core/csharp/stubs
#   - assembly_valheim.dll       : compiled from the checked-in ValheimStubs.cs
#
# ValheimStubs.cs is hand-written signatures for the Valheim members this mod
# binds against. It is the only stub source that still lives here; the Unity
# ones moved to core so the fleet stops carrying fifteen drifting copies.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsPath    = Join-Path $projectRoot 'src\ValheimHeadTracking\libs'
$vendorZip   = Join-Path $projectRoot 'vendor\bepinex\BepInEx_win_x64.zip'
$gameStubs   = Join-Path $libsPath 'ValheimStubs.cs'
$stubBuilder = Join-Path $projectRoot 'cameraunlock-core\csharp\stubs\build-unity-stubs.ps1'

if (-not (Test-Path $vendorZip))   { throw "Vendored BepInEx not found at $vendorZip" }
if (-not (Test-Path $gameStubs))   { throw "ValheimStubs.cs not found at $libsPath" }
if (-not (Test-Path $stubBuilder)) { throw "Shared stub builder not found at $stubBuilder - is the cameraunlock-core submodule checked out?" }

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

Write-Host "Populating libs/ from repo files (no game install required)..." -ForegroundColor Cyan

# Clean slate - libs/ holds only generated build refs (gitignored), so a local
# build reproduces CI's empty-libs start instead of masking it with stale DLLs.
Get-ChildItem -Path $libsPath -Force |
    Where-Object { $_.Name -ne 'ValheimStubs.cs' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# BepInEx from the vendored zip.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$tempDir = Join-Path $env:TEMP ("valheim-bep-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
try {
    [System.IO.Compression.ZipFile]::ExtractToDirectory($vendorZip, $tempDir)
    foreach ($dll in @('BepInEx.dll', '0Harmony.dll')) {
        $src = Join-Path $tempDir "BepInEx\core\$dll"
        if (-not (Test-Path $src)) { throw "$dll not found in vendor zip at BepInEx\core\" }
        Copy-Item $src (Join-Path $libsPath $dll) -Force
        Write-Host "  BepInEx: $dll" -ForegroundColor Gray
    }
} finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

& $stubBuilder -OutputPath $libsPath -TargetFramework net48 `
    -ExtraAssembly "assembly_valheim=$gameStubs"
if ($LASTEXITCODE -ne 0) { throw "Shared Unity stub build failed" }

Write-Host "Build dependencies ready." -ForegroundColor Green
