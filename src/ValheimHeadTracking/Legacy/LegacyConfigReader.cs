using System.IO;
using BepInEx.Configuration;

namespace ValheimHeadTracking.Legacy
{
    /// <summary>
    /// The plugin's BepInEx Bind calls as the build before the canonical config ran them: core's
    /// HeadTrackingConfigBase.Initialize and then the plugin's own OnInitialize, in that order, each
    /// definition's section, key, type, description and acceptable values unchanged and its default
    /// taken from <see cref="LegacyConfig"/>. Frozen for the life of the repo: it is how a player's
    /// .cfg is read, whichever earlier build wrote it.
    /// <para>
    /// It writes nothing. BepInEx's ConfigFile read the .cfg in its constructor, before whoever
    /// calls this held the file, so saving on set is turned off first and the file is read again
    /// before anything is bound. A missing file reads as the defaults.
    /// </para>
    /// </summary>
    internal static class LegacyConfigReader
    {
        /// <summary>Reads <paramref name="config"/>'s file into a new <see cref="LegacyConfig"/>.</summary>
        /// <param name="found">Whether the file existed.</param>
        public static LegacyConfig Read(ConfigFile config, out bool found)
        {
            config.SaveOnConfigSet = false;
            found = File.Exists(config.ConfigFilePath);
            if (found)
            {
                config.Reload();
            }

            var read = new LegacyConfig();

            read.UdpPort = config.Bind(
                "Network",
                "UdpPort",
                read.UdpPort,
                new ConfigDescription(
                    "UDP port for OpenTrack data. Requires game restart to take effect.",
                    new AcceptableValueRange<int>(1024, 65535)
                )
            ).Value;

            read.EnableOnStartup = config.Bind(
                "General",
                "EnableOnStartup",
                read.EnableOnStartup,
                "Enable head tracking automatically when the game loads"
            ).Value;

            read.YawSensitivity = config.Bind(
                "Sensitivity",
                "YawSensitivity",
                read.YawSensitivity,
                new ConfigDescription(
                    "Yaw (left/right) sensitivity multiplier",
                    new AcceptableValueRange<float>(0.1f, 3.0f)
                )
            ).Value;

            read.PitchSensitivity = config.Bind(
                "Sensitivity",
                "PitchSensitivity",
                read.PitchSensitivity,
                new ConfigDescription(
                    "Pitch (up/down) sensitivity multiplier",
                    new AcceptableValueRange<float>(0.1f, 3.0f)
                )
            ).Value;

            read.RollSensitivity = config.Bind(
                "Sensitivity",
                "RollSensitivity",
                read.RollSensitivity,
                new ConfigDescription(
                    "Roll (tilt) sensitivity multiplier",
                    new AcceptableValueRange<float>(0.1f, 3.0f)
                )
            ).Value;

            read.InvertYaw = config.Bind(
                "Inversion",
                "InvertYaw",
                read.InvertYaw,
                "Invert yaw axis. When enabled, looking left moves camera right."
            ).Value;

            read.InvertPitch = config.Bind(
                "Inversion",
                "InvertPitch",
                read.InvertPitch,
                "Invert pitch axis. When enabled, looking up moves camera down."
            ).Value;

            read.InvertRoll = config.Bind(
                "Inversion",
                "InvertRoll",
                read.InvertRoll,
                "Invert roll axis. When enabled, tilting left tilts camera right."
            ).Value;

            read.ToggleKey = config.Bind(
                "Hotkeys",
                "ToggleKey",
                read.ToggleKey,
                "Key to toggle head tracking on/off"
            ).Value;

            read.PositionToggleKey = config.Bind(
                "Hotkeys",
                "PositionToggleKey",
                read.PositionToggleKey,
                "Key to toggle position (6DOF) tracking on/off"
            ).Value;

            read.ReticleToggleKey = config.Bind(
                "Hotkeys",
                "ReticleToggleKey",
                read.ReticleToggleKey,
                "Key to toggle the decoupled aim reticle on/off"
            ).Value;

            read.EnableAimDecoupling = config.Bind(
                "Aim Decoupling",
                "EnableAimDecoupling",
                read.EnableAimDecoupling,
                "Decouple aim direction from head tracking. When enabled, projectiles and attacks " +
                "go where your mouse aims, not where your head is looking."
            ).Value;

            read.ShowDecoupledCrosshair = config.Bind(
                "Aim Decoupling",
                "ShowDecoupledCrosshair",
                read.ShowDecoupledCrosshair,
                "Move crosshair to show actual aim position when decoupled. When disabled, " +
                "crosshair stays centered but aim still goes to mouse position."
            ).Value;

            read.PositionLimitY = config.Bind(
                "Position",
                "PositionLimitY",
                read.PositionLimitY,
                new ConfigDescription(
                    "Maximum upward vertical displacement in meters",
                    new AcceptableValueRange<float>(0f, 1.5f))).Value;

            read.PositionLimitYDown = config.Bind(
                "Position",
                "PositionLimitYDown",
                read.PositionLimitYDown,
                new ConfigDescription(
                    "Maximum downward vertical displacement in meters",
                    new AcceptableValueRange<float>(0f, 1.5f))).Value;

            read.LocalSmoothing = config.Bind(
                "Smoothing",
                "LocalSmoothing",
                read.LocalSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker runs on this machine (loopback). 0 = no smoothing, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f))).Value;

            read.RemoteSmoothing = config.Bind(
                "Smoothing",
                "RemoteSmoothing",
                read.RemoteSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker is a remote device on the network. 0 = no smoothing, 1 = heavy.",
                    new AcceptableValueRange<float>(0f, 1f))).Value;

            read.WorldSpaceYaw = config.Bind(
                "General",
                "WorldSpaceYaw",
                read.WorldSpaceYaw,
                "Yaw mode: true = horizon-locked yaw (default), false = camera-local. " +
                "Horizon-locked keeps yaw around the world up-axis at any pitch. Camera-local " +
                "rotates around the view's current up-axis, which produces leaning at extreme pitches.").Value;

            read.YawModeKey = config.Bind(
                "Hotkeys",
                "YawModeKey",
                read.YawModeKey,
                "Key to toggle yaw mode (world-locked vs camera-local)").Value;

            return read;
        }
    }
}
