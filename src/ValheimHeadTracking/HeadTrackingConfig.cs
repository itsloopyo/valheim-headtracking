using System;
using BepInEx.Configuration;
using ValheimHeadTracking.Config;
using ValheimHeadTracking.Legacy;

namespace ValheimHeadTracking
{
    /// <summary>
    /// The settings the plugin runs on, read once at start from the plugin's .cfg by the frozen
    /// reader in Legacy/.
    /// </summary>
    public static class HeadTrackingConfig
    {
        private static ValheimConfig _current;
        private static ConfigEntry<bool> _worldSpaceYawEntry;
        private static ConfigEntry<bool> _showDecoupledCrosshairEntry;

        public static ValheimConfig Current =>
            _current ?? throw new InvalidOperationException(
                "HeadTrackingConfig.Initialize() has not been called. " +
                "Config must be initialized before accessing properties.");

        /// <summary>
        /// Must be called from plugin Awake() before other components are initialized.
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            bool found;
            _current = LegacyConfigMap.ToRuntime(LegacyConfigReader.Read(config, out found));

            // The reader writes nothing; this is the write BepInEx's Bind made on every start,
            // which creates the .cfg on the first one.
            config.SaveOnConfigSet = true;
            config.Save();

            // The yaw mode and reticle toggles write their entries back to the .cfg.
            if (!config.TryGetEntry("General", "WorldSpaceYaw", out _worldSpaceYawEntry)
                || !config.TryGetEntry("Aim Decoupling", "ShowDecoupledCrosshair", out _showDecoupledCrosshairEntry))
            {
                throw new InvalidOperationException("the legacy reader did not bind WorldSpaceYaw and ShowDecoupledCrosshair");
            }

            ValheimHeadTrackingPlugin.Log.LogInfo(
                $"Configuration initialized: Port={_current.UdpPort}, EnableOnStartup={_current.EnableOnStartup}");
        }

        public static void SetWorldSpaceYaw(bool value)
        {
            Current.WorldSpaceYaw = value;
            _worldSpaceYawEntry.Value = value;
        }

        public static void SetShowDecoupledCrosshair(bool value)
        {
            Current.ShowDecoupledCrosshair = value;
            _showDecoupledCrosshairEntry.Value = value;
        }
    }
}
