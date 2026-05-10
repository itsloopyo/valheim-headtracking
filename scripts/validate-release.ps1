#!/usr/bin/env pwsh
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "=== Valheim Head Tracking - Release Validation ===" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

foreach ($file in @("README.md", "CHANGELOG.md")) {
    Write-Host "Checking $file..." -ForegroundColor Gray
    if (Test-Path (Join-Path $projectRoot $file)) {
        Write-Host "  $file exists" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: $file not found" -ForegroundColor Red
        $allPassed = $false
    }
}

Write-Host "Checking build output..." -ForegroundColor Gray
$dllPath = Join-Path $projectRoot "src\ValheimHeadTracking\bin\Release\net48\ValheimHeadTracking.dll"
if (Test-Path $dllPath) {
    $dllInfo = Get-Item $dllPath
    Write-Host "  ValheimHeadTracking.dll exists ($($dllInfo.Length) bytes)" -ForegroundColor Green
} else {
    Write-Host "  WARNING: ValheimHeadTracking.dll not found" -ForegroundColor Yellow
    $allPassed = $false
}

Write-Host "Checking install.cmd MOD_VERSION matches csproj..." -ForegroundColor Gray
$csprojPath = Join-Path $projectRoot "src\ValheimHeadTracking\ValheimHeadTracking.csproj"
$installCmdPath = Join-Path $projectRoot "scripts\install.cmd"
if ((Test-Path $csprojPath) -and (Test-Path $installCmdPath)) {
    $csprojContent = Get-Content $csprojPath -Raw
    $csprojMatch = [regex]::Match($csprojContent, '<Version>([^<]+)</Version>')
    $installContent = Get-Content $installCmdPath -Raw
    $installMatch = [regex]::Match($installContent, '(?m)^set "MOD_VERSION=([^"]+)"')
    if (-not $csprojMatch.Success) {
        Write-Host "  ERROR: could not parse <Version> from csproj" -ForegroundColor Red
        $allPassed = $false
    } elseif (-not $installMatch.Success) {
        Write-Host "  ERROR: could not parse MOD_VERSION from install.cmd" -ForegroundColor Red
        $allPassed = $false
    } elseif ($csprojMatch.Groups[1].Value -ne $installMatch.Groups[1].Value) {
        Write-Host "  ERROR: csproj=$($csprojMatch.Groups[1].Value) but install.cmd=$($installMatch.Groups[1].Value)" -ForegroundColor Red
        Write-Host "  Run 'pixi run release <version>' or update install.cmd MOD_VERSION manually." -ForegroundColor Yellow
        $allPassed = $false
    } else {
        Write-Host "  Versions match: $($csprojMatch.Groups[1].Value)" -ForegroundColor Green
    }
} else {
    Write-Host "  ERROR: csproj or install.cmd not found" -ForegroundColor Red
    $allPassed = $false
}

Write-Host ""
Write-Host "===============================" -ForegroundColor Cyan

if ($allPassed) {
    Write-Host "All validation checks passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some validation checks failed." -ForegroundColor Yellow
    exit 1
}
