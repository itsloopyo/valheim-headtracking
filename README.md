# Valheim Head Tracking

![Valheim running with this mod](https://raw.githubusercontent.com/itsloopyo/valheim-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for Valheim that moves the camera with your head while your mouse or controller keeps aiming, driven by OpenTrack over UDP, with no VR headset required.

> **Settings have moved.** This version keeps its settings in `BepInEx\config\CameraUnlock.ini`.
> The first time it starts it reads your settings from the old
> `BepInEx\config\com.cameraunlock.valheim.headtracking.cfg` into the new file, and leaves
> the old file as it was. BepInEx's ConfigurationManager no longer lists the settings: edit
> `CameraUnlock.ini` with any text editor. [Configuration](#configuration) has the details.

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse.
- **6DOF positional tracking** - lean, peek, and duck with head position in addition to yaw / pitch / roll.
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android
- **Cycle tracking modes** - one key cycles between full 6DOF, 3DOF rotation only, and 3DOF position only.

## Requirements

- [Valheim](https://store.steampowered.com/app/892970/Valheim/) (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11 (64-bit)

## Installation

### Lopari

Download [Lopari](https://lopari.app), choose **Valheim**, and click
**Play with head tracking**.

### Standalone Installer

1. Download the latest installer ZIP from the [Releases page](https://github.com/itsloopyo/valheim-headtracking/releases).
2. Extract the ZIP anywhere.
3. Double-click `install.cmd`.
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`.
5. Launch the game.

The installer finds your game via Steam registry lookup and installs BepInEx if it is not already present. If it can't find the game:

- Set the `VALHEIM_PATH` environment variable to your game folder, or
- Run from a command prompt: `install.cmd "D:\Games\Valheim"`

### Manual Installation

If you prefer to place files by hand, or the installer can't locate your game:

1. Install [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) (`BepInEx_win_x64` ZIP) by extracting it into your Valheim folder.
2. Run Valheim once to let BepInEx initialize, then close it.
3. Grab the `-nexus.zip` variant from the Releases page and extract it into your Valheim folder. The DLLs land at `BepInEx\plugins\ValheimHeadTracking\`:
   - `ValheimHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`
   - `CameraUnlock.Core.Unity.BepInEx.dll`

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Two equivalent binding sets - use whichever your keyboard has. The chord letters sit in the middle of the keyboard so they work on laptops without a nav cluster. These are the default keys: `ToggleKey`, `CycleTrackingModeKey` and `YawModeKey` in `CameraUnlock.ini` list the keys for each action, chords included (see [Configuration](#configuration)).

The tracking-mode cycle steps through **6DOF (rotation + position) -> 3DOF rotation only -> 3DOF position only -> 6DOF**. Use the master toggle (`End` / `Ctrl+Shift+Y`) to turn tracking off entirely.

| Action                     | Nav-cluster | Chord           |
|----------------------------|-------------|-----------------|
| Toggle head tracking       | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode        | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode            | `Page Down` | `Ctrl+Shift+H`  |

The tracking mode and the yaw mode you pick are saved to `CameraUnlock.ini`, so the next start begins with them. The master toggle changes the current session only: each start has head tracking on or off as `EnableOnStartup` says.

The game's crosshair, and the name of whatever you are looking at, move to where your aim points while head tracking turns the view. There is no setting that turns this off.

## Configuration

<!-- cameraunlock:config -->
The mod reads its settings from `BepInEx\config\CameraUnlock.ini` in the game folder, and creates the file when it starts and finds none. Edit it with any text editor.

A setting set to `default` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.

`Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.

When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that. Edit it with any text editor.

Earlier versions of the mod kept these settings in `com.cameraunlock.valheim.headtracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `com.cameraunlock.valheim.headtracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `com.cameraunlock.valheim.headtracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.

A setting that the defaults below set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it. `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.

Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:

- Reticle settings, and a key that toggled the reticle.
- A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
- The setting for a feature that earlier versions shipped switched off while it was untested. It now follows the mod's default.

An older version of the mod reads `com.cameraunlock.valheim.headtracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `com.cameraunlock.valheim.headtracking.cfg`.

Deleting only `CameraUnlock.ini` makes the next start read `com.cameraunlock.valheim.headtracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults below. Every setting they set to `default` then follows `Defaults.ini`.

On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `com.cameraunlock.valheim.headtracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.

BepInEx's ConfigurationManager no longer lists these settings.

The built-in value of each setting set to `default` below:

- `UdpPort=4242`
- `EnableOnStartup=true`
- `WorldSpaceYaw=true`
- `RotationEnabled=true`
- `LocalSmoothing=0.0`
- `RemoteSmoothing=0.15`
- `PositionEnabled=true`
- `PositionLimitY=0.2`
- `PositionLimitYDown=0.2`
- `ToggleKey=End, Ctrl+Shift+Y`
- `CycleTrackingModeKey=PageUp, Ctrl+Shift+G`
- `YawModeKey=PageDown, Ctrl+Shift+H`

With every setting at its default, the file reads:

```ini
; Valheim head tracking settings.
; Comments start with ; and go on their own line. Text after a value is part of the value.
; Hotkeys are key names such as End, PageUp or Ctrl+Shift+Y. Separate several with commas; leave empty for none.
; A setting set to default takes its value from Defaults.ini, which every head tracking mod
; that keeps its settings in CameraUnlock.ini reads: %AppData%\CameraUnlock\Defaults.ini on
; Windows, $XDG_CONFIG_HOME/CameraUnlock/Defaults.ini (normally ~/.config/CameraUnlock) on
; Linux, under Wine and Proton too, and ~/Library/Application Support/CameraUnlock/Defaults.ini
; on macOS. The log names the file it read. Write a value instead of default to change that
; setting for this game only.

[CameraUnlock]
; Written by the mod. Leave this section in place.
ConfigFormat=1

[Network]
; UDP port the mod receives tracker data on (OpenTrack protocol).
UdpPort=default

[General]
; true: head tracking is on when the game starts. ToggleKey turns it on and off.
EnableOnStartup=default
; true: yaw turns around the world's up axis. false: around the camera's own up axis.
WorldSpaceYaw=default
; true: turning your head turns the view.
; Tracking mode at startup, with PositionEnabled. The mode hotkey changes both.
RotationEnabled=default

[Smoothing]
; Smoothing when the tracker runs on this PC. 0 is the least, 1 the most.
LocalSmoothing=default
; Smoothing when the tracker is another device on the network, such as a phone.
; 0 is the least, 1 the most.
RemoteSmoothing=default

[Position]
; true: moving your head moves the view.
; Tracking mode at startup, with RotationEnabled. The mode hotkey changes both.
PositionEnabled=default
; How far, in metres, raising your head can move the view.
PositionLimitY=default
; How far, in metres, lowering your head can move the view.
PositionLimitYDown=default

[Hotkeys]
; Turns head tracking on and off.
ToggleKey=default
; Changes the tracking mode: rotation and position, rotation only, position only.
CycleTrackingModeKey=default
; Switches yaw between the world's up axis and the camera's own (WorldSpaceYaw).
YawModeKey=default
```
<!-- /cameraunlock:config -->

Smoothing covers both rotation and position. Which of the two values applies is
decided per connection from the packet source address: a tracker running on this
PC uses `LocalSmoothing`, a phone or other network device uses `RemoteSmoothing`.

The mod reads `CameraUnlock.ini` when the game starts, so restart the game after
editing it.

## Troubleshooting

**Mod not loading:**
- Verify BepInEx is installed (look for `BepInEx\LogOutput.log` after running the game once).
- Ensure all four DLLs are in `BepInEx\plugins\ValheimHeadTracking\`.
- Check `BepInEx\LogOutput.log` for error messages.

**No tracking response:**
- Confirm OpenTrack (or your phone app) is running and actively sending data.
- Verify UDP output is set to `127.0.0.1:4242` (or your PC's LAN IP if tracking from a phone).
- Press **End** to make sure tracking is enabled.
- Check your firewall isn't blocking UDP port 4242.

**View sits off-centre:**
- Centre it in your tracker app: OpenTrack's Center bind, or the CENTER button in a phone tracker app. The mod keeps no centre of its own, it applies the pose the tracker sends.

**A config edit had no effect:**
- Restart the game: the mod reads `CameraUnlock.ini` when the game starts.
- Make sure nothing follows the value on the line: text after a value is part of the value. `BepInEx\LogOutput.log` names each line the mod could not read and the value it used instead.

**Jittery / unstable tracking:**
- Raise `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in `CameraUnlock.ini` toward `0.3`-`0.5`, with nothing after the value on the line.
- Increase filtering in OpenTrack (Accela filter recommended).
- Improve lighting for webcam-based tracking.
- On WiFi phone tracking, some jitter is expected - the mod's built-in smoothing helps but cannot fully compensate for heavy packet loss.

**Wrong rotation axis:**
- Invert that axis in OpenTrack's output mapping. This mod has no invert or sensitivity settings; the axis corrections it needs are applied internally and are not configurable.

**Yaw feels wrong when looking up or down at extreme angles:**
- Try toggling between world-locked and camera-local yaw with `Page Down` (or `Ctrl+Shift+H`). World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis.

## Updating

Download the new release and run `install.cmd` again. Your `CameraUnlock.ini` is kept.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs and leaves `BepInEx\config\CameraUnlock.ini` and the old `.cfg` in place. The mod loader (BepInEx) is only removed if the installer put it there. To force-remove BepInEx:

```
uninstall.cmd /force
```

## Building from Source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (any recent version)
- [pixi](https://pixi.sh) task runner
- Valheim installed (for Unity/BepInEx DLL references)

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/valheim-headtracking.git
cd valheim-headtracking

# Build and install to game
pixi run install

# Build only
pixi run build

# Package for release
pixi run package
```

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [Iron Gate AB](https://www.irongatestudio.se/) - Valheim
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [Harmony](https://github.com/pardeike/Harmony) - Runtime patching library
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking software

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Iron Gate AB. It modifies only what the local client renders and does not change aim, hit detection, or any game logic - server-authoritative behavior is untouched. Use at your own risk, and be considerate of other players when running it in multiplayer sessions.
