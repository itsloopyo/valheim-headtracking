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

        /// <summary>
        /// PositionLimitY as v0.1.0 to v0.1.3 shipped it. BepInEx never rewrites a value already in
        /// the .cfg, so a player who first ran one of those builds holds it untouched today.
        /// </summary>
        public const float ShippedBeforeV014PositionLimitY = 0.15f;

        /// <summary>PositionLimitYDown as v0.1.0 to v0.1.3 shipped it.</summary>
        public const float ShippedBeforeV014PositionLimitYDown = 0.05f;

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
            var follows = new LegacyFollowsDefaultsIni();
            Map(legacy, config, dropped, poseShaping, follows);
            return found
                ? ImportResult.Imported(dropped, poseShaping, follows.Concepts)
                : ImportResult.Absent(dropped, poseShaping, follows.Concepts);
        }

        /// <summary>
        /// Every float the reader returns is inside its AcceptableValueRange, which BepInEx clamps
        /// NaN and infinity into, so no value reaches here that normalisation N2 would change.
        /// </summary>
        /// <param name="follows">Given every row of the table that follows Defaults.ini, left there
        /// where the .cfg holds what v0.3.0 shipped (the frozen <see cref="LegacyConfig"/> defaults),
        /// or for a vertical limit, what v0.1.0 to v0.1.3 shipped.</param>
        public static void Map(LegacyConfig legacy, ValheimConfig config, List<DroppedValue> dropped,
            List<PoseShapingValue> poseShaping, LegacyFollowsDefaultsIni follows)
        {
            var shipped = new LegacyConfig();

            config.UdpPort = legacy.UdpPort;
            config.EnableOnStartup = legacy.EnableOnStartup;
            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            follows.Setting(ConfigConcepts.UdpPort, legacy.UdpPort, shipped.UdpPort);
            follows.Setting(ConfigConcepts.EnableOnStartup, legacy.EnableOnStartup, shipped.EnableOnStartup);
            follows.Setting(ConfigConcepts.WorldSpaceYaw, legacy.WorldSpaceYaw, shipped.WorldSpaceYaw);

            config.ToggleKeyName = HotkeyList(legacy.ToggleKey, KeyCode.Y, "ToggleKey", dropped);
            config.CycleTrackingModeKeyName = HotkeyList(legacy.PositionToggleKey, KeyCode.G, "PositionToggleKey", dropped);
            config.YawModeKeyName = HotkeyList(legacy.YawModeKey, KeyCode.H, "YawModeKey", dropped);
            follows.Setting(ConfigConcepts.ToggleKey, legacy.ToggleKey, shipped.ToggleKey);
            follows.Setting(ConfigConcepts.CycleTrackingModeKey, legacy.PositionToggleKey, shipped.PositionToggleKey);
            follows.Setting(ConfigConcepts.YawModeKey, legacy.YawModeKey, shipped.YawModeKey);

            // The game's crosshair now always follows the aim, and the aim is always decoupled.
            // EnableAimDecoupling is the aim decoupling switch under its BepInEx spelling, and
            // ShowDecoupledCrosshair only stopped the crosshair following. A player who kept
            // either at its shipped true loses nothing, and the reticle key is gone for all.
            if (legacy.ReticleToggleKey != KeyCode.None)
            {
                dropped.Add(new DroppedValue(DropRule.Reticle, "Hotkeys", "ReticleToggleKey", KeyText((int)legacy.ReticleToggleKey)));
            }
            if (!legacy.EnableAimDecoupling)
            {
                dropped.Add(new DroppedValue(DropRule.CoupledAim, "Aim Decoupling", "EnableAimDecoupling", "false"));
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
            // No build had a setting for it, so no player chose it.
            config.RotationEnabled = true;
            config.PositionEnabled = true;
            follows.TrackingMode(true);

            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            PositionSettings p = config.Position;
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                p.LimitX, legacy.PositionLimitY, legacy.PositionLimitYDown, p.LimitZ, p.LimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);
            follows.Setting(ConfigConcepts.LocalSmoothing, legacy.LocalSmoothing, shipped.LocalSmoothing);
            follows.Setting(ConfigConcepts.RemoteSmoothing, legacy.RemoteSmoothing, shipped.RemoteSmoothing);
            follows.Setting(ConfigConcepts.PositionLimitY,
                legacy.PositionLimitY == shipped.PositionLimitY || legacy.PositionLimitY == ShippedBeforeV014PositionLimitY);
            follows.Setting(ConfigConcepts.PositionLimitYDown,
                legacy.PositionLimitYDown == shipped.PositionLimitYDown || legacy.PositionLimitYDown == ShippedBeforeV014PositionLimitYDown);
        }

        /// <summary>
        /// The keys v0.3.0 fired an action on: the configured key, unless it was None, and the
        /// Ctrl+Shift chord HotkeyHandler checked beside it. A Ctrl, Shift or Alt key alone is
        /// unbound under N3 and recorded, and the chord stays. So is a key code Unity names no key
        /// for (a number in the .cfg, which BepInEx's enum parse accepts), recorded as
        /// KeyCodeOutOfRange (N1).
        /// </summary>
        public static string HotkeyList(KeyCode primary, KeyCode chordLetter, string key, List<DroppedValue> dropped)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            string text = LegacyNormalisations.KeyCodeToBindings((int)primary, "Hotkeys", key, dropped);
            return text.Length == 0 ? chord : text + ", " + chord;
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
