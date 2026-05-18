# Changelog

## [0.1.4] - 2026-05-18

### Other

- Enable readme demo clip
- Cycle tracking modes and widen vertical position limits
- scripts: drop the two-phase loader-init prompt from install bodies
- data: add Black & White (Lionhead) to games registry
- scripts: detect BepInEx 6 IL2CPP via BepInEx.Core.dll marker
- powershell: skip cameraunlock-core remote refresh in CI
- scripts: add UE4SS install template, fix delayed expansion in ASI body, expand games registry


## [0.1.3] - 2026-05-12

Release 0.1.3.

## [0.1.2] - 2026-05-12

### Other

- Set PLUGIN_SUBFOLDER for Valheim plugin layout
- Add PLUGIN_SUBFOLDER support to BepInEx install/uninstall bodies

## [0.1.1] - 2026-05-12

### Other

- Use camera basis vectors for position offset

## [0.1.0] - 2026-05-10

### Other

- Hello world

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
