using UnityEngine;

namespace ValheimHeadTracking.Config
{
    /// <summary>The settings the plugin runs on.</summary>
    public sealed class ValheimConfig
    {
        public int UdpPort { get; set; }
        public bool EnableOnStartup { get; set; }

        public float YawSensitivity { get; set; }
        public float PitchSensitivity { get; set; }
        public float RollSensitivity { get; set; }

        public bool InvertYaw { get; set; }
        public bool InvertPitch { get; set; }
        public bool InvertRoll { get; set; }

        public KeyCode ToggleKey { get; set; }
        public KeyCode PositionToggleKey { get; set; }
        public KeyCode ReticleToggleKey { get; set; }

        public bool EnableAimDecoupling { get; set; }
        public bool ShowDecoupledCrosshair { get; set; }

        public float PositionLimitY { get; set; }
        public float PositionLimitYDown { get; set; }

        public float LocalSmoothing { get; set; }
        public float RemoteSmoothing { get; set; }

        public bool WorldSpaceYaw { get; set; }
        public KeyCode YawModeKey { get; set; }
    }
}
