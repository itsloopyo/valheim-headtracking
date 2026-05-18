# Valheim Head Tracking

An unofficial flatscreen head tracking mod for Valheim that decouples looking from aiming, so you can glance around the world with your head while your mouse still controls where you aim.

![Mod GIF](https://raw.githubusercontent.com/itsloopyo/valheim-headtracking/main/assets/readme-clip.gif)

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse.
- **6DOF positional tracking** - lean, peek, and duck with head position in addition to yaw / pitch / roll.
- **Cycle tracking modes** - one key cycles between full 6DOF, 3DOF rotation only, and 3DOF position only.

## Requirements

- [Valheim](https://store.steampowered.com/app/892970/Valheim/) (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11 (64-bit)

## Installation

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

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Configure your tracker as input.
3. Set output to **UDP over network**.
4. Host: `127.0.0.1`, Port: `4242`.
5. Start tracking before launching the game.

### Webcam Setup

No special hardware needed - OpenTrack's built-in **neuralnet tracker** uses any webcam for 6DOF face tracking.

1. In OpenTrack, set the input to **neuralnet tracker**.
2. Select your webcam in the tracker settings.
3. Set output to **UDP over network** (`127.0.0.1:4242`).
4. Start tracking before launching the game.
5. Recenter in OpenTrack via its hotkey, and press **Home** in-game to recenter the mod as needed.

### Phone App Setup

This mod includes built-in smoothing for network jitter, so you can send directly from your phone on port 4242 without running OpenTrack on your PC.

1. Install an OpenTrack-compatible head tracking app.
2. Configure it to send to your PC's IP on port 4242 (run `ipconfig` to find it).
3. Set the protocol to OpenTrack/UDP.

**With OpenTrack (optional):** If you want curve mapping or visual preview, route through OpenTrack. Set OpenTrack's input to "UDP over network" on a different port (e.g. 5252), point your phone app at that port, and set OpenTrack's output to `127.0.0.1:4242`. Make sure your firewall allows incoming UDP on the input port.

## Controls

Two equivalent binding sets - use whichever your keyboard has. The chord letters sit in the middle of the keyboard so they work on laptops without a nav cluster.

The tracking-mode cycle steps through **6DOF (rotation + position) -> 3DOF rotation only -> 3DOF position only -> 6DOF**. Use the master toggle (`End` / `Ctrl+Shift+Y`) to turn tracking off entirely.

| Action                     | Nav-cluster | Chord           |
|----------------------------|-------------|-----------------|
| Recenter view              | `Home`      | `Ctrl+Shift+T`  |
| Toggle head tracking       | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode        | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode            | `Page Down` | `Ctrl+Shift+H`  |
| Toggle aim reticle         | `Insert`    | `Ctrl+Shift+U`  |

## Configuration

The mod creates a config file at `Valheim\BepInEx\config\com.cameraunlock.valheim.headtracking.cfg` on first run. Edit it to customize:

```ini
[General]
EnableOnStartup = true           # Start with tracking enabled
WorldSpaceYaw = true             # true = horizon-locked yaw (default), false = camera-local

[Network]
UdpPort = 4242                   # Must match OpenTrack output port

[Hotkeys]
ToggleKey = End
RecenterKey = Home
PositionToggleKey = PageUp
ReticleToggleKey = Insert
YawModeKey = PageDown

[Sensitivity]
YawSensitivity = 1.0             # Horizontal rotation (0.1-3.0)
PitchSensitivity = 1.0           # Vertical rotation (0.1-3.0)
RollSensitivity = 1.0            # Head tilt (0.1-3.0)

[Inversion]
InvertYaw = false
InvertPitch = false
InvertRoll = false

[Aim Decoupling]
EnableAimDecoupling = true       # Separate aim from head movement
ShowDecoupledCrosshair = true    # Move crosshair to the actual aim position

[Position]
PositionLimitY = 0.60            # Max upward vertical offset in meters (0.0-1.5)
PositionLimitYDown = 0.40        # Max downward vertical offset in meters (0.0-1.5)
```

Delete the file to reset all settings to defaults.

## Troubleshooting

**Mod not loading:**
- Verify BepInEx is installed (look for `BepInEx\LogOutput.log` after running the game once).
- Ensure all four DLLs are in `BepInEx\plugins\ValheimHeadTracking\`.
- Check `BepInEx\LogOutput.log` for error messages.

**No tracking response:**
- Confirm OpenTrack (or your phone app) is running and actively sending data.
- Verify UDP output is set to `127.0.0.1:4242` (or your PC's LAN IP if tracking from a phone).
- Press **End** to make sure tracking is enabled, then **Home** to recenter.
- Check your firewall isn't blocking UDP port 4242.

**Jittery / unstable tracking:**
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

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [Iron Gate AB](https://www.irongatestudio.se/) - Valheim
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [Harmony](https://github.com/pardeike/Harmony) - Runtime patching library
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking software

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Iron Gate AB. It modifies only what the local client renders and does not change aim, hit detection, or any game logic - server-authoritative behavior is untouched. Use at your own risk, and be considerate of other players when running it in multiplayer sessions.
