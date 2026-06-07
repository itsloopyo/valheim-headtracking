#!/usr/bin/env pwsh
# Populates src/ValheimHeadTracking/libs/ for a game-free build, from REPO
# FILES ONLY - no Valheim install required - so `pixi run package` builds
# identically locally and in CI:
#   - BepInEx.dll / 0Harmony.dll : extracted from the vendored BepInEx zip
#   - assembly_valheim.dll + UnityEngine.*.dll : committed Refasmer metadata-only
#     reference assemblies (vendor/game-refs/). These carry the REAL public API
#     signatures of the game DLLs (fields stay fields, etc.) with no IL bodies,
#     so the mod compiles against shapes that match the real assemblies at
#     runtime. Regenerate from a real install with `pixi run update-game-refs`.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsPath    = Join-Path $projectRoot 'src\ValheimHeadTracking\libs'
$vendorZip   = Join-Path $projectRoot 'vendor\bepinex\BepInEx_win_x64.zip'
$gameRefs    = Join-Path $projectRoot 'vendor\game-refs'

if (-not (Test-Path $vendorZip)) { throw "Vendored BepInEx not found at $vendorZip" }
if (-not (Test-Path $gameRefs)) {
    throw "Game reference assemblies not found at $gameRefs. Run 'pixi run update-game-refs' against a Valheim install."
}

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

Write-Host "Populating libs/ from repo files (no game install required)..." -ForegroundColor Cyan

# Clean slate - libs/ holds only generated build refs (gitignored), so a local
# build reproduces CI's empty-libs start instead of masking it with stale DLLs.
Get-ChildItem -Path $libsPath -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

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

# Refasmer reference assemblies for the Unity + Valheim DLLs.
Get-ChildItem -Path $gameRefs -Filter *.dll | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $libsPath $_.Name) -Force
    Write-Host "  Ref: $($_.Name)" -ForegroundColor Gray
}

Write-Host "Build dependencies ready." -ForegroundColor Green
