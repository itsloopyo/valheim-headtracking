using ValheimHeadTracking.Config;

namespace ValheimHeadTracking.Legacy
{
    /// <summary>Carries what <see cref="LegacyConfigReader"/> read into the settings the plugin runs on.</summary>
    internal static class LegacyConfigMap
    {
        public static ValheimConfig ToRuntime(LegacyConfig legacy)
        {
            return new ValheimConfig
            {
                UdpPort = legacy.UdpPort,
                EnableOnStartup = legacy.EnableOnStartup,
                YawSensitivity = legacy.YawSensitivity,
                PitchSensitivity = legacy.PitchSensitivity,
                RollSensitivity = legacy.RollSensitivity,
                InvertYaw = legacy.InvertYaw,
                InvertPitch = legacy.InvertPitch,
                InvertRoll = legacy.InvertRoll,
                ToggleKey = legacy.ToggleKey,
                PositionToggleKey = legacy.PositionToggleKey,
                ReticleToggleKey = legacy.ReticleToggleKey,
                EnableAimDecoupling = legacy.EnableAimDecoupling,
                ShowDecoupledCrosshair = legacy.ShowDecoupledCrosshair,
                PositionLimitY = legacy.PositionLimitY,
                PositionLimitYDown = legacy.PositionLimitYDown,
                LocalSmoothing = legacy.LocalSmoothing,
                RemoteSmoothing = legacy.RemoteSmoothing,
                WorldSpaceYaw = legacy.WorldSpaceYaw,
                YawModeKey = legacy.YawModeKey,
            };
        }
    }
}
