#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Valheim Head Tracking mod.

.DESCRIPTION
    Canonical 8-step release workflow:
      1. Validate semver arg.
      2. Verify on main branch, clean tree, tag doesn't exist.
      3. Update version in csproj + PLUGIN_VERSION.
      4. pixi run build (release config). Abort on failure.
      5. Generate CHANGELOG from commits since last tag.
      6. Commit "Release v<version>" with version bump + changelog.
      7. Create annotated tag v<version>.
      8. Push commits + tag (triggers .github/workflows/release.yml).

.PARAMETER Version
    The version to release (e.g., "1.0.0", "1.2.3").

.EXAMPLE
    pixi run release 1.0.0

.NOTES
    Run via: pixi run release <version>
#>
param(
    [Parameter(Position=0)]
    [string]$Version = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\ValheimHeadTracking\ValheimHeadTracking.csproj"
$pluginSourcePath = Join-Path $projectDir "src\ValheimHeadTracking\ValheimHeadTrackingPlugin.cs"
$pixiTomlPath = Join-Path $projectDir "pixi.toml"
$installCmdPath = Join-Path $projectDir "scripts\install.cmd"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

Write-Host "=== Valheim Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CsprojVersion $csprojPath

# If no version provided, show current and exit
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <major|minor|patch|X.Y.Z>" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release patch" -ForegroundColor White
    exit 0
}

# Step 1: Resolve major/minor/patch into a concrete version (or accept literal X.Y.Z)
try {
    $Version = Resolve-ReleaseVersion -Argument $Version -CurrentVersion $currentVersion
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$tagName = "v$Version"

# Step 2: Pre-flight git checks
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

if (-not (Test-CleanGitStatus)) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host "Please commit or stash changes before releasing" -ForegroundColor Yellow
    exit 1
}

if (Test-GitTagExists -Tag $tagName) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 3: Update version
Write-Host "Updating version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion -CsprojPath $csprojPath -Version $Version

$pluginContent = Get-Content $pluginSourcePath -Raw
$pluginContent = $pluginContent -replace 'PLUGIN_VERSION = "[^"]+"', "PLUGIN_VERSION = `"$Version`""
$pluginContent | Set-Content $pluginSourcePath -NoNewline
Write-Host "  Updated PLUGIN_VERSION in ValheimHeadTrackingPlugin.cs" -ForegroundColor Gray

$pixiContent = Get-Content $pixiTomlPath -Raw
$pixiContent = [regex]::Replace($pixiContent, '(?m)^version\s*=\s*"[^"]+"', "version = `"$Version`"", 1)
$pixiContent | Set-Content $pixiTomlPath -NoNewline
Write-Host "  Updated workspace version in pixi.toml" -ForegroundColor Gray

# install.cmd's MOD_VERSION is written to the per-game state file. If it drifts
# from the csproj, users get an installed mod whose state file lies about the
# version - confusing for support and for the launcher's update detection.
# Read/write as bytes to preserve the CRLF line endings .cmd requires.
$installBytes = [System.IO.File]::ReadAllBytes($installCmdPath)
$installText = [System.Text.Encoding]::ASCII.GetString($installBytes)
$updatedInstallText = [regex]::Replace(
    $installText,
    '(?m)^set "MOD_VERSION=[^"]*"',
    "set `"MOD_VERSION=$Version`"",
    1)
if ($updatedInstallText -eq $installText) {
    Write-Host "Error: Could not find 'set ""MOD_VERSION=...""' line in install.cmd" -ForegroundColor Red
    exit 1
}
[System.IO.File]::WriteAllBytes(
    $installCmdPath,
    [System.Text.Encoding]::ASCII.GetBytes($updatedInstallText))
Write-Host "  Updated MOD_VERSION in scripts/install.cmd" -ForegroundColor Gray

# Step 4: Build to verify the bump compiles
Write-Host "Building release..." -ForegroundColor Cyan
Push-Location $projectDir
try {
    pixi run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Build failed. Reverting is up to you (version files are staged-for-commit only)." -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

# Step 5: Generate CHANGELOG
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
$hasExistingTags = git tag -l 2>$null
if (-not $hasExistingTags) {
    $date = Get-Date -Format 'yyyy-MM-dd'
    $firstEntry = "# Changelog`n`n## [$Version] - $date`n`nFirst release.`n"
    Set-Content $changelogPath $firstEntry -NoNewline
    Write-Host "  First release - wrote initial CHANGELOG entry" -ForegroundColor Gray
} else {
    try {
        New-ChangelogFromCommits `
            -ChangelogPath $changelogPath `
            -Version $Version `
            -ArtifactPaths @("src/", "scripts/", "vendor/", "cameraunlock-core", "README.md", "THIRD-PARTY-NOTICES.md")
    } catch {
        Write-Host "  No usable commits in range, writing manual changelog entry" -ForegroundColor Yellow
        $date = Get-Date -Format 'yyyy-MM-dd'
        $entry = "## [$Version] - $date`n`nRelease $Version.`n"
        $existing = if (Test-Path $changelogPath) { Get-Content $changelogPath -Raw } else { "# Changelog`n`n" }
        $existing = $existing -replace '(# Changelog\s*)', "`$1`n$entry`n"
        Set-Content $changelogPath $existing -NoNewline
    }
}

# Step 6: Commit
Write-Host "Committing version change..." -ForegroundColor Cyan
git add $csprojPath $pluginSourcePath $pixiTomlPath $installCmdPath $changelogPath
git commit -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Commit failed." -ForegroundColor Red
    exit 1
}

# Step 7: Annotated tag
Write-Host "Creating annotated tag $tagName..." -ForegroundColor Cyan
git tag -a $tagName -m "Release $tagName"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Tag creation failed." -ForegroundColor Red
    exit 1
}

# Step 8: Push
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to push commits. Tag created locally." -ForegroundColor Red
    Write-Host "Run manually: git push origin main && git push origin $tagName" -ForegroundColor Yellow
    exit 1
}
git push origin $tagName
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to push tag. Run manually: git push origin $tagName" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Release $tagName initiated!" -ForegroundColor Green
Write-Host ""
Write-Host "The GitHub Actions release workflow will now:" -ForegroundColor Yellow
Write-Host "  - Build the release" -ForegroundColor White
Write-Host "  - Create GitHub release with artifacts" -ForegroundColor White
Write-Host ""
Write-Host "Watch progress at:" -ForegroundColor Yellow
Write-Host "  https://github.com/itsloopyo/valheim-headtracking/actions" -ForegroundColor Cyan
