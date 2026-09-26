using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using UnityEngine;
using ValheimHeadTracking.Config;

namespace ValheimHeadTracking.Legacy
{
    /// <summary>
    /// The import the config owner runs on com.cameraunlock.valheim.headtracking.cfg while
    /// CameraUnlock.ini is absent: <see cref="LegacyConfigReader"/> on the plugin's own ConfigFile,
    /// then the map into <see cref="ValheimConfig"/>.
    /// </summary>
    internal static class LegacyConfigImport
    {
        /// <summary>The rotation multiplier every published build shipped on all three axes.</summary>
        public const float ShippedSensitivity = 1.0f;

        /// <summary>The axis inversion every published build shipped on all three axes.</summary>
        public const bool ShippedInversion = false;

        /// <param name="pluginConfig">The plugin's Config, whose file is the legacy file.</param>
        public static LegacyImport<ValheimConfig> For(ConfigFile pluginConfig)
        {
            return new LegacyImport<ValheimConfig>((input, config) => Run(pluginConfig, input, config), LegacyConfigKeys.All());
        }

        public static ImportResult Run(ConfigFile pluginConfig, LegacyImportInput input, ValheimConfig config)
        {
            if (!string.Equals(Path.GetFullPath(input.Path), Path.GetFullPath(pluginConfig.ConfigFilePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("the owner hands over " + input.Path + ", and the plugin's ConfigFile reads "
                                                    + pluginConfig.ConfigFilePath);
            }

            bool found;
            LegacyConfig legacy = LegacyConfigReader.Read(pluginConfig, out found);
            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            Map(legacy, config, dropped, poseShaping);
            return found ? ImportResult.Imported(dropped, poseShaping) : ImportResult.Absent(dropped, poseShaping);
        }

        /// <summary>
        /// Every float the reader returns is inside its AcceptableValueRange, which BepInEx clamps
        /// NaN and infinity into, so no value reaches here that normalisation N2 would change.
        /// </summary>
        public static void Map(LegacyConfig legacy, ValheimConfig config, List<DroppedValue> dropped,
            List<PoseShapingValue> poseShaping)
        {
            config.UdpPort = legacy.UdpPort;
            config.EnableOnStartup = legacy.EnableOnStartup;
            config.WorldSpaceYaw = legacy.WorldSpaceYaw;

            config.ToggleKeyName = HotkeyList(legacy.ToggleKey, KeyCode.Y);
            config.CycleTrackingModeKeyName = HotkeyList(legacy.PositionToggleKey, KeyCode.G);
            config.YawModeKeyName = HotkeyList(legacy.YawModeKey, KeyCode.H);

            // The game's crosshair now always follows the aim. Both switches only stopped it
            // following: the aim itself stayed on the mouse whatever they held. So a player who
            // kept either at its shipped true loses nothing, and the reticle key is gone for all.
            if (legacy.ReticleToggleKey != KeyCode.None)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "Hotkeys", "ReticleToggleKey", KeyText((int)legacy.ReticleToggleKey)));
            }
            if (!legacy.EnableAimDecoupling)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "Aim Decoupling", "EnableAimDecoupling", "false"));
            }
            if (!legacy.ShowDecoupledCrosshair)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "Aim Decoupling", "ShowDecoupledCrosshair", "false"));
            }

            LegacyPoseShaping.Record(legacy.YawSensitivity, ShippedSensitivity, "Sensitivity", "YawSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, ShippedSensitivity, "Sensitivity", "PitchSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, ShippedSensitivity, "Sensitivity", "RollSensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertYaw, ShippedInversion, "Inversion", "InvertYaw", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertPitch, ShippedInversion, "Inversion", "InvertPitch", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertRoll, ShippedInversion, "Inversion", "InvertRoll", poseShaping, dropped);

            // The published builds started every session in rotation and position, and the cycle
            // key changed the mode for that session only.
            config.RotationEnabled = true;
            config.PositionEnabled = true;

            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            PositionSettings p = config.Position;
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                p.LimitX, legacy.PositionLimitY, legacy.PositionLimitYDown, p.LimitZ, p.LimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);
        }

        /// <summary>
        /// The keys v0.3.0 fired an action on: the configured key, unless it was None, and the
        /// Ctrl+Shift chord HotkeyHandler checked beside it. A key code Unity names no key for
        /// (a number in the .cfg, which BepInEx's enum parse accepts) is written as that number,
        /// which no hotkey list reads, so the owner defers the import and says which line.
        /// </summary>
        public static string HotkeyList(KeyCode primary, KeyCode chordLetter)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            if (primary == KeyCode.None) return chord;
            return KeyText((int)primary) + ", " + chord;
        }

        private static string KeyText(int unityKeyCode)
        {
            try
            {
                return KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.None, unityKeyCode) });
            }
            catch (ArgumentException)
            {
                return unityKeyCode.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
