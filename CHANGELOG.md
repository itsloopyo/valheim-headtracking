# Changelog

## [1.0.6] - 2026-05-01

### Changed
- Made vendored BepInEx the single install-time source of truth.
- Bundled vendored BepInEx in the installer ZIP.
- Polished license file, refreshed vendored loader, and tightened install scripts.

### Fixed
- Fixed /y flag propagation in install.cmd and uninstall.cmd for unattended installs.
- Fixed .cmd output races against Windows Defender by using atomic file writes.

## [1.0.5] - 2026-02-24

### Fixed
- Fixed release ZIP filename to include v prefix.

## [1.0.2] - 2026-02-24

### Fixed
- Fixed PowerShell operator precedence bug in package script.

## [1.0.0] - 2026-02-24

### Added
- Initial release of Valheim Head Tracking mod.
- Added head tracking support via OpenTrack UDP protocol.
- Added configurable sensitivity and smoothing.
- Added in-game toggle and recenter controls.
- Deploys to BepInEx\plugins\ValheimHeadTracking\ subfolder.
