# Uninstall ValheimHeadTracking mod from BepInEx
$ErrorActionPreference = "Stop"

# Get Valheim path from environment or use default Steam location
$valheimPath = $env:VALHEIM_PATH
if (-not $valheimPath) {
    $valheimPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
}

$pluginsPath = Join-Path $valheimPath "BepInEx\plugins"
$targetPath = Join-Path $pluginsPath "ValheimHeadTracking"

if (-not (Test-Path $targetPath)) {
    Write-Host "Mod not installed at $targetPath"
    exit 0
}

Write-Host "Uninstalling from $targetPath"
Remove-Item -Recurse -Force $targetPath
Write-Host "Uninstall complete!"
