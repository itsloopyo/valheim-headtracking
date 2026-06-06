using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Tracking;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Static wrapper that owns the shared UDP receiver and HeadTrackingSession
    /// (receiver -> interpolators -> processors) for the lifetime of the plugin.
    /// The session provides pose interpolation, hold-on-tracking-loss, and
    /// stabilized auto-recenter on tracker connection.
    /// </summary>
    public static class OpenTrackReceiver
    {
        // Valheim's third-person camera sits far from the player model, so positional
        // tracking needs boosted sensitivity and wider limits than the core defaults
        // for leaning to be perceptible.
        private const float PositionSensitivity = 2.0f;
        private const float PositionLimitX = 0.60f;
        private const float PositionLimitZ = 0.80f;
        private const float PositionLimitZBack = 0.60f;
        private const float PositionSmoothing = 0.15f;

        private static CameraUnlock.Core.Protocol.OpenTrackReceiver _receiver;
        private static TrackingProcessor _processor;
        private static PositionProcessor _positionProcessor;
        private static HeadTrackingSession _session;

        /// <summary>
        /// The per-frame tracking pipeline. Null until <see cref="Start"/> is called.
        /// </summary>
        public static HeadTrackingSession Session => _session;

        /// <summary>
        /// True if the UDP socket failed to bind (port in use or other error).
        /// </summary>
        public static bool IsFailed => _receiver?.IsFailed ?? false;

        /// <summary>
        /// True if packets have been received within the last 500ms.
        /// </summary>
        public static bool IsReceiving => _receiver?.IsReceiving ?? false;

        /// <summary>
        /// Starts the UDP receiver on the specified port and builds the tracking session.
        /// If the port is in use, the core receiver logs the failure and retries in the
        /// background - callers can observe state via <see cref="IsFailed"/> / <see cref="IsReceiving"/>.
        /// </summary>
        public static void Start(int port)
        {
            if (_receiver != null)
            {
                ValheimHeadTrackingPlugin.Log.LogWarning("OpenTrackReceiver already running");
                return;
            }

            _receiver = new CameraUnlock.Core.Protocol.OpenTrackReceiver();
            _receiver.Log = msg => ValheimHeadTrackingPlugin.Log.LogInfo(msg);

            _processor = new TrackingProcessor { SmoothingFactor = 0f };
            _positionProcessor = new PositionProcessor();
            _session = new HeadTrackingSession(_receiver, _processor, _positionProcessor)
            {
                Log = msg => ValheimHeadTrackingPlugin.Log.LogInfo(msg)
            };
            UpdateProcessorSettings();

            if (_receiver.Start(port))
            {
                ValheimHeadTrackingPlugin.Log.LogInfo($"OpenTrackReceiver started on 0.0.0.0:{port}");
            }
        }

        /// <summary>
        /// Updates the processor settings from config values.
        /// Call this when config values change.
        /// </summary>
        public static void UpdateProcessorSettings()
        {
            if (_processor == null) return;

            // Configure sensitivity with inversion
            // Note: Pitch needs negation by default (OpenTrack up = Unity down)
            // Config.InvertPitch=false means "natural" = negate, so we invert the flag
            _processor.Sensitivity = new SensitivitySettings(
                HeadTrackingConfig.CachedYawSensitivity,
                HeadTrackingConfig.CachedPitchSensitivity,
                HeadTrackingConfig.CachedRollSensitivity,
                HeadTrackingConfig.CachedInvertYaw,
                !HeadTrackingConfig.CachedInvertPitch,  // Inverted: default needs negation
                HeadTrackingConfig.CachedInvertRoll
            );

            _positionProcessor.Settings = new PositionSettings(
                PositionSensitivity, PositionSensitivity, PositionSensitivity,
                PositionLimitX,
                HeadTrackingConfig.PositionLimitY.Value,
                HeadTrackingConfig.PositionLimitYDown.Value,
                PositionLimitZ, PositionLimitZBack,
                PositionSmoothing,
                invertX: true, invertY: false, invertZ: true);
        }

        /// <summary>
        /// Stops the UDP receiver and releases resources.
        /// </summary>
        public static void Stop()
        {
            if (_receiver == null) return;

            _receiver.Dispose();
            _receiver = null;
            _processor = null;
            _positionProcessor = null;
            _session = null;
            ValheimHeadTrackingPlugin.Log.LogInfo("OpenTrackReceiver stopped");
        }

        /// <summary>
        /// Sets the current head pose and position as the new center.
        /// Caller MUST check IsReceiving before calling this method.
        /// </summary>
        public static void Recenter()
        {
            _session.Recenter();
            ValheimHeadTrackingPlugin.Log.LogInfo("Head tracking recentered");
        }
    }
}
