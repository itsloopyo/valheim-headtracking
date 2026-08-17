#!/usr/bin/env pwsh
# Populates src/ValheimHeadTracking/libs/ for a game-free build, from REPO
# FILES ONLY - no Valheim install required - so `pixi run package` builds
# identically locally and in CI:
#   - BepInEx.dll / 0Harmony.dll : extracted from the vendored BepInEx zip
#   - UnityEngine*.dll           : compiled from the checked-in UnityStubs.cs
#   - UnityEngine.UI.dll         : compiled from the checked-in UnityUIStubs.cs
#   - assembly_valheim.dll       : compiled from the checked-in ValheimStubs.cs
#
# The stub sources are hand-written signatures for the members this mod binds
# against, so the build needs nothing beyond this repo.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsPath    = Join-Path $projectRoot 'src\ValheimHeadTracking\libs'
$vendorZip   = Join-Path $projectRoot 'vendor\bepinex\BepInEx_win_x64.zip'
$unityStubs  = Join-Path $libsPath 'UnityStubs.cs'
$uiStubs     = Join-Path $libsPath 'UnityUIStubs.cs'
$gameStubs   = Join-Path $libsPath 'ValheimStubs.cs'

if (-not (Test-Path $vendorZip))  { throw "Vendored BepInEx not found at $vendorZip" }
if (-not (Test-Path $unityStubs)) { throw "UnityStubs.cs not found at $libsPath" }
if (-not (Test-Path $uiStubs))    { throw "UnityUIStubs.cs not found at $libsPath" }
if (-not (Test-Path $gameStubs))  { throw "ValheimStubs.cs not found at $libsPath" }

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null

Write-Host "Populating libs/ from repo files (no game install required)..." -ForegroundColor Cyan

# Clean slate - libs/ holds only generated build refs (gitignored), so a local
# build reproduces CI's empty-libs start instead of masking it with stale DLLs.
Get-ChildItem -Path $libsPath -Force |
    Where-Object { $_.Name -notin @('UnityStubs.cs', 'UnityUIStubs.cs', 'ValheimStubs.cs') } |
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

function Build-Stub {
    param(
        [string]$assemblyName,
        [string]$compileItem,
        [string[]]$references = @()
    )

    $refItems = ($references | ForEach-Object {
        "    <Reference Include=`"$([System.IO.Path]::GetFileNameWithoutExtension($_))`"><HintPath>$_</HintPath><Private>false</Private></Reference>"
    }) -join "`n"

    $proj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AssemblyName>$assemblyName</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0169;CS0649;CS0067;CS0660;CS0661</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$compileItem" />
$refItems
  </ItemGroup>
</Project>
"@
    $projPath = Join-Path $libsPath "Stub_$assemblyName.csproj"
    $proj | Out-File -FilePath $projPath -Encoding utf8
    dotnet build $projPath -c Release -o $libsPath --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build stub $assemblyName" }
    Remove-Item $projPath -ErrorAction SilentlyContinue
    Write-Host "  Stub: $assemblyName.dll" -ForegroundColor Gray
}

# Every stubbed engine type lives in UnityStubs.cs and therefore in
# UnityEngine.dll; the module assemblies exist only so references resolve. That
# works because the shipped UnityEngine.dll type-forwards every module type.
Build-Stub 'UnityEngine' 'UnityStubs.cs'

# uGUI ships as its own assembly with no forwarder from UnityEngine.dll, so its
# stubs have to be compiled into UnityEngine.UI.dll or the emitted typerefs
# point at an assembly that does not declare them.
Build-Stub 'UnityEngine.UI' 'UnityUIStubs.cs' @('UnityEngine.dll')

# The game stubs bind against both, so they compile after them.
Build-Stub 'assembly_valheim' 'ValheimStubs.cs' @('UnityEngine.dll', 'UnityEngine.UI.dll')

$emptySource = Join-Path $libsPath 'EmptyStub.cs'
'// Empty stub assembly' | Out-File -FilePath $emptySource -Encoding utf8
foreach ($m in @(
    'UnityEngine.CoreModule', 'UnityEngine.IMGUIModule', 'UnityEngine.PhysicsModule',
    'UnityEngine.TextRenderingModule', 'UnityEngine.InputLegacyModule',
    'UnityEngine.UIModule'
)) { Build-Stub $m 'EmptyStub.cs' }

Remove-Item $emptySource -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath '*.deps.json') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $libsPath '*.pdb')        -Force -ErrorAction SilentlyContinue

Write-Host "Build dependencies ready." -ForegroundColor Green
