using UnityEngine;

namespace ValheimHeadTracking.Legacy
{
    /// <summary>
    /// The settings v0.3.0 and every earlier build read from
    /// BepInEx\config\com.cameraunlock.valheim.headtracking.cfg, with their defaults. Frozen: a later
    /// change to the runtime settings or to core's constants must never change what an old .cfg, or
    /// one missing a key, reads as. The defaults are literals for that reason, although the reader
    /// this was frozen from took the port and the smoothing pair from core, which held these same
    /// values.
    /// </summary>
    internal sealed class LegacyConfig
    {
        public int UdpPort = 4242;
        public bool EnableOnStartup = true;

        public float YawSensitivity = 1.0f;
        public float PitchSensitivity = 1.0f;
        public float RollSensitivity = 1.0f;

        public bool InvertYaw = false;
        public bool InvertPitch = false;
        public bool InvertRoll = false;

        public KeyCode ToggleKey = KeyCode.End;
        public KeyCode PositionToggleKey = KeyCode.PageUp;
        public KeyCode ReticleToggleKey = KeyCode.Insert;

        public bool EnableAimDecoupling = true;
        public bool ShowDecoupledCrosshair = true;

        public float PositionLimitY = 0.60f;
        public float PositionLimitYDown = 0.40f;

        public float LocalSmoothing = 0.0f;
        public float RemoteSmoothing = 0.15f;

        public bool WorldSpaceYaw = true;
        public KeyCode YawModeKey = KeyCode.PageDown;
    }
}
