using CameraUnlock.Core.Config;

namespace ValheimHeadTracking.Config
{
    /// <summary>
    /// Everything the mod reads from BepInEx\config\CameraUnlock.ini. Unity-free, so the test
    /// project compiles it and holds the committed file to it.
    /// </summary>
    public sealed class ValheimConfig : HeadTrackingConfigData
    {
        /// <summary>The game's name as data/games.json spells it.</summary>
        public const string DisplayName = "Valheim";

        public static ConfigTable<ValheimConfig> Table()
        {
            return HeadTrackingConfigTable.Create<ValheimConfig>(
                    ConfigConcepts.UdpPort,
                    ConfigConcepts.EnableOnStartup,
                    ConfigConcepts.WorldSpaceYaw,
                    ConfigConcepts.RotationEnabled,
                    ConfigConcepts.LocalSmoothing,
                    ConfigConcepts.RemoteSmoothing,
                    ConfigConcepts.PositionEnabled,
                    ConfigConcepts.PositionLimitY,
                    ConfigConcepts.PositionLimitYDown,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.WorldSpaceYaw).Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable();
        }
    }
}
