# Setup libs folder for ValheimHeadTracking
# Copies required DLLs from Valheim installation
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsDir = Join-Path $projectRoot "src\ValheimHeadTracking\libs"

$bepinexDlls = @(
    "0Harmony.dll",
    "BepInEx.dll"
)

$managedDlls = @(
    "assembly_valheim.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.UIModule.dll"
)

# Idempotency guard: if every required DLL is already present, exit without
# touching the filesystem. Lets `build` depend on this task without re-copying
# every invocation, and lets contributors without Valheim installed build as
# long as libs/ has been pre-populated.
if (Test-Path $libsDir) {
    $missing = @($bepinexDlls + $managedDlls) | Where-Object {
        -not (Test-Path (Join-Path $libsDir $_))
    }
    if ($missing.Count -eq 0) {
        Write-Host "libs/ already populated -- skipping setup."
        exit 0
    }
}

# Get Valheim path from environment or use default Steam location
$valheimPath = $env:VALHEIM_PATH
if (-not $valheimPath) {
    $valheimPath = "C:\Program Files (x86)\Steam\steamapps\common\Valheim"
}

$managedPath = Join-Path $valheimPath "valheim_Data\Managed"
$bepinexPath = Join-Path $valheimPath "BepInEx\core"

if (-not (Test-Path $managedPath)) {
    Write-Error "Valheim Managed folder not found at $managedPath"
    Write-Error "Set VALHEIM_PATH environment variable to your Valheim installation"
    exit 1
}

if (-not (Test-Path $bepinexPath)) {
    Write-Error "BepInEx not found at $bepinexPath"
    Write-Error "Make sure BepInEx is installed in your Valheim folder"
    exit 1
}

if (-not (Test-Path $libsDir)) {
    New-Item -ItemType Directory -Path $libsDir | Out-Null
}

Write-Host "Copying DLLs to $libsDir"

foreach ($dll in $bepinexDlls) {
    $source = Join-Path $bepinexPath $dll
    if (Test-Path $source) {
        Copy-Item $source $libsDir -Force
        Write-Host "  Copied $dll (BepInEx)"
    } else {
        Write-Warning "  Not found: $source"
    }
}

foreach ($dll in $managedDlls) {
    $source = Join-Path $managedPath $dll
    if (Test-Path $source) {
        Copy-Item $source $libsDir -Force
        Write-Host "  Copied $dll (Managed)"
    } else {
        Write-Warning "  Not found: $source"
    }
}

Write-Host "Setup complete!"
