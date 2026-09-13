# Valheim Head Tracking

![Valheim running with this mod](https://raw.githubusercontent.com/itsloopyo/valheim-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for Valheim that moves the camera with your head while your mouse or controller keeps aiming, driven by OpenTrack over UDP, with no VR headset required.

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

Two equivalent binding sets - use whichever your keyboard has. The chord letters sit in the middle of the keyboard so they work on laptops without a nav cluster.

The tracking-mode cycle steps through **6DOF (rotation + position) -> 3DOF rotation only -> 3DOF position only -> 6DOF**. Use the master toggle (`End` / `Ctrl+Shift+Y`) to turn tracking off entirely.

| Action                     | Nav-cluster | Chord           |
|----------------------------|-------------|-----------------|
| Toggle head tracking       | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode        | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode            | `Page Down` | `Ctrl+Shift+H`  |
| Toggle aim reticle         | `Insert`    | `Ctrl+Shift+U`  |

## Configuration

The mod creates a config file at `Valheim\BepInEx\config\com.cameraunlock.valheim.headtracking.cfg` on first run. Edit it to customize:

A comment has to sit on its own line. BepInEx splits each line at the first `=`
and takes everything after it as the value, so a trailing `# note` becomes part
of the value, the conversion fails, and the entry silently keeps its default -
the only trace is a line in `BepInEx\LogOutput.log`. Put explanations above the
key, never after it.

```ini
[General]
# Start with tracking enabled
EnableOnStartup = true
# true = horizon-locked yaw (default), false = camera-local
WorldSpaceYaw = true

[Network]
# Must match OpenTrack output port
UdpPort = 4242

[Hotkeys]
ToggleKey = End
# Cycles 6DOF -> 3DOF rotation only -> 3DOF position only
PositionToggleKey = PageUp
ReticleToggleKey = Insert
YawModeKey = PageDown

[Sensitivity]
# Horizontal rotation (0.1-3.0)
YawSensitivity = 1.0
# Vertical rotation (0.1-3.0)
PitchSensitivity = 1.0
# Head tilt (0.1-3.0)
RollSensitivity = 1.0

[Inversion]
InvertYaw = false
InvertPitch = false
InvertRoll = false

[Aim Decoupling]
# Separate aim from head movement
EnableAimDecoupling = true
# Move crosshair to the actual aim position
ShowDecoupledCrosshair = true

[Position]
# Max upward vertical offset in meters (0.0-1.5)
PositionLimitY = 0.60
# Max downward vertical offset in meters (0.0-1.5)
PositionLimitYDown = 0.40

[Smoothing]
# Smoothing when the tracker runs on this machine (0.0-1.0)
LocalSmoothing = 0.0
# Smoothing when the tracker is a remote network device (0.0-1.0)
RemoteSmoothing = 0.15
```

Smoothing covers both rotation and position. Which of the two values applies is
decided per connection from the packet source address: a tracker running on this
PC uses `LocalSmoothing`, a phone or other network device uses `RemoteSmoothing`.
Switching between them takes effect without restarting the game.

Delete the file to reset all settings to defaults.

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
- Make sure nothing follows the value on the line. A trailing `# comment` is read as part of the value, the entry falls back to its default, and the game gives no sign of it. `BepInEx\LogOutput.log` records the failed conversion.

**Jittery / unstable tracking:**
- Raise `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) toward `0.3`-`0.5`, with nothing after the value on the line.
- Increase filtering in OpenTrack (Accela filter recommended).
- Reduce sensitivity in the mod config.
- Improve lighting for webcam-based tracking.
- On WiFi phone tracking, some jitter is expected - the mod's built-in smoothing helps but cannot fully compensate for heavy packet loss.

**Wrong rotation axis:**
- Flip `InvertYaw`, `InvertPitch`, or `InvertRoll` in the config file.
- Or adjust curves directly in OpenTrack.

**Yaw feels wrong when looking up or down at extreme angles:**
- Try toggling between world-locked and camera-local yaw with `Page Down` (or `Ctrl+Shift+H`). World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis.

## Updating

Download the new release and run `install.cmd` again. Your config is preserved.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs. The mod loader (BepInEx) is only removed if the installer put it there. To force-remove BepInEx:

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
