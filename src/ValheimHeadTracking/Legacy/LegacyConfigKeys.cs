using CameraUnlock.Core.Config;

namespace ValheimHeadTracking.Legacy
{
    /// <summary>Every section and key <see cref="LegacyConfigReader"/> reads. Frozen with it.</summary>
    internal static class LegacyConfigKeys
    {
        public static LegacyKey[] All()
        {
            return new[]
            {
                new LegacyKey("Network", "UdpPort"),
                new LegacyKey("General", "EnableOnStartup"),
                new LegacyKey("Sensitivity", "YawSensitivity"),
                new LegacyKey("Sensitivity", "PitchSensitivity"),
                new LegacyKey("Sensitivity", "RollSensitivity"),
                new LegacyKey("Inversion", "InvertYaw"),
                new LegacyKey("Inversion", "InvertPitch"),
                new LegacyKey("Inversion", "InvertRoll"),
                new LegacyKey("Hotkeys", "ToggleKey"),
                new LegacyKey("Hotkeys", "PositionToggleKey"),
                new LegacyKey("Hotkeys", "ReticleToggleKey"),
                new LegacyKey("Aim Decoupling", "EnableAimDecoupling"),
                new LegacyKey("Aim Decoupling", "ShowDecoupledCrosshair"),
                new LegacyKey("Position", "PositionLimitY"),
                new LegacyKey("Position", "PositionLimitYDown"),
                new LegacyKey("Smoothing", "LocalSmoothing"),
                new LegacyKey("Smoothing", "RemoteSmoothing"),
                new LegacyKey("General", "WorldSpaceYaw"),
                new LegacyKey("Hotkeys", "YawModeKey"),
            };
        }
    }
}
