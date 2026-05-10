using System;
using System.Runtime.CompilerServices;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Static wrapper around CameraUnlock.Core.Protocol.OpenTrackReceiver and TrackingProcessor.
    /// Provides the same static API that Valheim mod code expects while delegating
    /// to the shared library for UDP reception, processing, and lock-free data access.
    /// </summary>
    public static class OpenTrackReceiver
    {
        private static CameraUnlock.Core.Protocol.OpenTrackReceiver _receiver;
        private static TrackingProcessor _processor;

        /// <summary>
        /// True if the UDP socket failed to bind (port in use or other error).
        /// </summary>
        public static bool IsFailed => _receiver?.IsFailed ?? false;

        /// <summary>
        /// True if packets have been received within the last 500ms.
        /// </summary>
        public static bool IsReceiving
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _receiver?.IsReceiving ?? false;
        }

        /// <summary>
        /// Starts the UDP receiver on the specified port and initializes the processor.
        /// If the port is in use, the core receiver logs the failure and retries in the
        /// background — callers can observe state via <see cref="IsFailed"/> / <see cref="IsReceiving"/>.
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
            UpdateProcessorSettings();

            if (_receiver.Start(port))
            {
                ValheimHeadTrackingPlugin.Log.LogInfo($"OpenTrackReceiver started on 0.0.0.0:{port}");
            }
        }

        /// <summary>
        /// Updates the processor settings from cached config values.
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
            ValheimHeadTrackingPlugin.Log.LogInfo("OpenTrackReceiver stopped");
        }

        /// <summary>
        /// Gets the processed rotation values using TrackingProcessor.
        /// Applies center offset, deadzone, sensitivity, inversion, and limits.
        /// Caller MUST check IsReceiving before calling this method.
        /// </summary>
        /// <param name="deltaTime">Frame delta time for smoothing calculations.</param>
        /// <returns>Processed rotation tuple (Yaw, Pitch, Roll) in degrees.</returns>
        /// <exception cref="InvalidOperationException">Thrown if receiver is not started.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (float Yaw, float Pitch, float Roll) GetProcessedRotation(float deltaTime)
        {
            EnsureStarted();
            var rawPose = _receiver.GetLatestPose();
            var processed = _processor.Process(rawPose, deltaTime);
            return (processed.Yaw, processed.Pitch, processed.Roll);
        }

        /// <summary>
        /// Gets the raw rotation values without sensitivity or inversion applied.
        /// Caller MUST check IsReceiving before calling this method.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if receiver is not started.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (float Yaw, float Pitch, float Roll) GetRawRotation()
        {
            EnsureStarted();
            _receiver.GetRawRotation(out float yaw, out float pitch, out float roll);
            return (yaw, pitch, roll);
        }

        /// <summary>
        /// Sets the current head position as the new center point.
        /// Uses the processor's CenterOffsetManager for proper offset tracking.
        /// Caller MUST check IsReceiving before calling this method.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if receiver is not started.</exception>
        public static void Recenter()
        {
            EnsureStarted();
            var rawPose = _receiver.GetLatestPose();
            _processor.RecenterTo(rawPose);
            ValheimHeadTrackingPlugin.Log.LogInfo($"Recentered at Yaw={rawPose.Yaw:F2}, Pitch={rawPose.Pitch:F2}, Roll={rawPose.Roll:F2}");
        }

        /// <summary>
        /// Gets the latest position data from the receiver.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PositionData GetLatestPosition()
        {
            EnsureStarted();
            return _receiver.GetLatestPosition();
        }

        /// <summary>
        /// Resets the processor state (clears center offset and smoothing).
        /// </summary>
        public static void Reset()
        {
            _processor?.Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EnsureStarted()
        {
            if (_receiver == null || _processor == null)
            {
                throw new InvalidOperationException(
                    "OpenTrackReceiver is not started. Call Start() first or check IsReceiving before invoking.");
            }
        }
    }
}
