#!/usr/bin/env pwsh
# Regenerate the committed Refasmer reference assemblies in vendor/game-refs/
# from a real Valheim install. Dev-only, like update-deps: run it when Valheim
# updates and the game DLLs' public API changes, then review + COMMIT the diff.
#
# The output is metadata-only (no IL) - just the public API surface the mod
# compiles against - the same defensible reference-assembly form used elsewhere
# in this org (see shadows-of-doubt's vendor/unity-ui-stub). It is NOT shipped
# to end users; it exists only so the build needs no Valheim install.
#
# Requires the Refasmer CLI: dotnet tool install -g JetBrains.Refasmer.CliTool

param([string]$ValheimPath = $env:VALHEIM_PATH)

$ErrorActionPreference = 'Stop'

if (-not $ValheimPath) {
    $ValheimPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
}
$managed = Join-Path $ValheimPath "valheim_Data\Managed"
if (-not (Test-Path $managed)) {
    throw "Valheim Managed folder not found at $managed. Install Valheim or set VALHEIM_PATH."
}
if (-not (Get-Command refasmer -ErrorAction SilentlyContinue)) {
    throw "refasmer not found. Install it: dotnet tool install -g JetBrains.Refasmer.CliTool"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$gameRefs  = Join-Path (Split-Path -Parent $scriptDir) 'vendor\game-refs'
New-Item -ItemType Directory -Path $gameRefs -Force | Out-Null
Get-ChildItem -Path $gameRefs -Filter *.dll -ErrorAction SilentlyContinue | Remove-Item -Force

# Exactly the game-derived DLLs ValheimHeadTracking.csproj references.
$dlls = @(
    'assembly_valheim.dll',
    'UnityEngine.dll',
    'UnityEngine.CoreModule.dll',
    'UnityEngine.IMGUIModule.dll',
    'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.PhysicsModule.dll',
    'UnityEngine.TextRenderingModule.dll',
    'UnityEngine.UI.dll',
    'UnityEngine.UIModule.dll'
)
foreach ($d in $dlls) {
    $src = Join-Path $managed $d
    if (-not (Test-Path $src)) { throw "$d not found in $managed" }
    refasmer --all -O $gameRefs $src
    if ($LASTEXITCODE -ne 0) { throw "refasmer failed for $d" }
    Write-Host "  Refreshed: $d" -ForegroundColor Green
}

Write-Host "vendor/game-refs/ regenerated from $managed. Review and commit the diff." -ForegroundColor Cyan
