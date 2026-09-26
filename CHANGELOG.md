# Changelog

## [Unreleased]

### Changed

- Settings move to `BepInEx\config\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `com.cameraunlock.valheim.headtracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `com.cameraunlock.valheim.headtracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `com.cameraunlock.valheim.headtracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- A setting that the defaults the README shows set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity or axis inversion you changed from its default. Set these in your tracker instead.
  - Reticle settings, and a key that toggled the reticle. Here that is `ReticleToggleKey`, `ShowDecoupledCrosshair` and `EnableAimDecoupling`: set to false, either of the last two only kept the game's crosshair at the centre of the screen, since your aim stayed on the mouse whatever they held.
- An older version of the mod reads `com.cameraunlock.valheim.headtracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `com.cameraunlock.valheim.headtracking.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `com.cameraunlock.valheim.headtracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- BepInEx's ConfigurationManager no longer lists these settings. Edit `BepInEx\config\CameraUnlock.ini` with any text editor, then restart the game: the mod reads it when the game starts.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`. `PositionToggleKey` becomes `CycleTrackingModeKey`, and your key for it is carried over.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `com.cameraunlock.valheim.headtracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.
- The tracking mode you pick with `Page Up` is saved to `CameraUnlock.ini` and is what the next start begins with. Earlier versions started every session in rotation and position. The yaw mode (`Page Down`) is saved as before, now to `CameraUnlock.ini`. `End` still changes the current session only.
- The game's crosshair, and the name of what you are looking at, always move to where your aim points while head tracking turns the view. Earlier versions let `ShowDecoupledCrosshair`, `EnableAimDecoupling` or `Insert` keep them at the centre of the screen.
- `PositionLimitY` and `PositionLimitYDown` in a new `CameraUnlock.ini` are `default`, whose built-in value is 0.2 for both, where earlier versions started at 0.6 and 0.4. A setting imported from `com.cameraunlock.valheim.headtracking.cfg` keeps the value it held there.
- `[Hotkeys] RecenterKey` is not carried over from `com.cameraunlock.valheim.headtracking.cfg`. v0.3.0, which removed the recenter hotkey, still read the key and acted on it nowhere; cameraunlock-core e92f4bf, taken in 479b1f5, stopped reading it.

### Added

- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.

### Removed

- The key that toggled the reticle (`Insert`, `Ctrl+Shift+U`), and the crosshair settings (`ReticleToggleKey`, `ShowDecoupledCrosshair`, `EnableAimDecoupling`).
- The sensitivity and axis inversion settings (`YawSensitivity`, `PitchSensitivity`, `RollSensitivity`, `InvertYaw`, `InvertPitch`, `InvertRoll`). Set these in your tracker app instead.
- With these settings at their shipped defaults the camera moves as it did before.

## [0.3.0] - 2026-08-20

### Added

- drop the mod-side centre and log the first tracker packet

### Fixed

- give the forward lean its own travel budget again

## [0.2.2] - 2026-08-18

### Fixed

- match stub member kinds to the shipped Unity assemblies
- match stub member kinds to the shipped Unity assemblies
- compile the uGUI stubs into UnityEngine.UI, not UnityEngine

## [0.2.1] - 2026-08-17

### Added

- follow core's split of SmoothingFactor into a per-connection pair

## [Unreleased]

### Added

- one latched `First tracker packet received on port N` line in `BepInEx\LogOutput.log`. Until now the log could not distinguish "the tracker never reached the mod" from "tracking was gated by the game state", which cost a round trip on every "no head tracking" report

### Changed

- the mod keeps no centre of its own. The recenter hotkey (`Home` / `Ctrl+Shift+T`) is gone, along with the in-game "Recentered" message. Every tracker app centres itself, so a mod-side centre sat in series with the tracker's and the two drifted apart. Centre in your tracker app instead: OpenTrack's Center bind, or the CENTER button in a phone tracker app
- add `LocalSmoothing` (default 0.0) and `RemoteSmoothing` (default 0.15) config keys, replacing the hardcoded rotation smoothing and the separate 0.15 position smoothing constant; the value is selected per connection from the packet source address and covers both rotation and position
- remove the hidden 0.15 baseline smoothing floor, so a tracker running on this PC now gets zero-latency tracking by default

## [0.2.0] - 2026-08-03

### Fixed

- show full control set in pixi install via shared -Controls

### Other

- Link Discord, Lopari and Headcam from the README

## [0.1.5] - 2026-06-07

### Added

- add HeadTrackingSession and expand C++ core with RE Engine, Unreal, and tracking-session modules
- aim projection, reframework/unreal hooks, input/logging hardening, games
- add Mass Effect Legendary Edition to games catalog
- expand games catalog, fix unicode games.json read, stage launcher manifest
- add Pacific Drive to games catalog
- add Homeworld: Remastered Collection to games catalog
- add manifest-mode installer validator and ASI loader subdir support
- authenticate GitHub API requests via env token when present
- add R.E.P.O. detection data

### Fixed

- fail fast in ASI dev-deploy when the game is running
- restore il2cpp camera position by undoing applied local delta
- set SO_REUSEADDR so the receiver reclaims its port on relaunch
- harden release.ps1 - changelog gate before version bump, add -Force

### Other

- protocol: reject finite-but-out-of-float-range packet values
- data: add Subnautica 2 to games registry
- detection: add installer-registry game path lookup (Black & White GameDir)
- protocol: reorder tracking data member in udp_receiver
- data: fix Subnautica 2 Steam app id (3367150 -> 1962700)
- data: add Ni no Kuni Remastered and Yakuza 0; switch find-game output to UTF-8
- detection: add Xbox/GDK build support for Subnautica 2 (and any future GDK title)
- find-game: escape `&` in GAME_DISPLAY_NAME so echo doesn't split
- templates: add uninstall.ps1; data: add Deus Ex Mankind Divided
- powershell: add NightlyRelease module for Patreon-gated nightly builds
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics; powershell: write nightly manifest.json without UTF-8 BOM; data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: publish dev builds as GitHub pre-releases
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics
- data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: run gh under Continue so its stderr doesn't abort the dev-release publish
- reframework: strip VR runtime DLLs on install for flatscreen mode
- reframework: cache GetValue method and avoid per-call heap in ArrayGetValue; data: add BioShock Infinite
- uninstall: remove reframework_revision.txt marker dropped at game root
- install: render MOD_CONTROLS multi-line via percent expansion
- Add YAPYAP to games.json
- powershell: write state file BOM-less so Lopari JSON parser accepts it
- Migrate tracking pipeline to shared HeadTrackingSession
- Make build game-free and route CI through pixi run package
- powershell: stop redirecting git stderr in Invoke-VersionCommit

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
