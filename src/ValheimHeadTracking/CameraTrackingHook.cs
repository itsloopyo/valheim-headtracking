using System.Runtime.CompilerServices;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.State;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;

namespace ValheimHeadTracking
{
    /// <summary>
    /// MonoBehaviour attached to the GameCamera to apply head tracking at the correct time.
    /// Uses OnPreCull() which runs just before the camera renders, after all LateUpdate() calls.
    ///
    /// Modifies the camera's worldToCameraMatrix (view matrix) rather than camera.transform,
    /// so game logic (raycasts, physics, aim) remains unaffected by head tracking.
    /// </summary>
    public sealed class CameraTrackingHook : MonoBehaviour
    {
        private Camera _camera;
        private Vector3 _lastAppliedRotation;
        private bool _wasTracking;

        // Position tracker is static because it must survive hook re-attachment across scene
        // transitions; eagerly initialized so no bootstrap call is needed.
        private static readonly PositionTracker Position = new PositionTracker();
        private static bool _wasReceiving;

        /// <summary>
        /// Gets the last applied tracking rotation (yaw, pitch, roll in degrees).
        /// Useful for aim decoupling calculations.
        /// </summary>
        public Vector3 LastAppliedRotation => _lastAppliedRotation;

        /// <summary>Resets position processing state (zeroes out offset).</summary>
        public static void ResetPosition() => Position.Reset();

        /// <summary>Recenters position to current head position.</summary>
        public static void RecenterPosition() => Position.Recenter();

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                ValheimHeadTrackingPlugin.Log.LogError("CameraTrackingHook: No Camera component found!");
            }
        }

        /// <summary>
        /// Called just before this camera renders.
        /// This is the key timing - it runs AFTER all LateUpdate() calls, so the game's
        /// camera code has already run and we can apply our tracking on top without
        /// being overwritten.
        /// </summary>
        private void OnPreCull()
        {
            // Early exits ordered by likelihood and cost
            if (!TrackingState.IsEnabled)
            {
                ResetIfTracking();
                return;
            }

            if (_camera == null) return;

            if (!OpenTrackReceiver.IsReceiving)
            {
                _wasReceiving = false;
                ResetIfTracking();
                return;
            }

            if (!_wasReceiving)
            {
                _wasReceiving = true;
                OpenTrackReceiver.Recenter();
                RecenterPosition();
                ValheimHeadTrackingPlugin.Log.LogInfo("Auto-recentered: tracking signal acquired");
            }

            if (!GameStateDetector.ShouldTrack())
            {
                ResetIfTracking();
                return;
            }

            ApplyTracking();
        }

        /// <summary>
        /// Resets the view matrix to auto-calculated mode if we were previously tracking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetIfTracking()
        {
            if (_wasTracking && _camera != null)
            {
                ViewMatrixModifier.Reset(_camera);
                _wasTracking = false;
            }
        }

        /// <summary>
        /// Applies head tracking to the camera's view matrix.
        /// Branches on HeadTrackingConfig.CachedWorldSpaceYaw:
        ///  - true (default): ApplyHeadRotationDecomposed, yaw around world up then local pitch/roll.
        ///  - false: ApplyHeadRotation, single camera-local YXZ quaternion (leans at extreme pitch).
        /// Camera.transform is never modified in either mode; aim decoupling still works.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyTracking()
        {
            // Cache Time.deltaTime once. Each access is a native property call.
            float dt = Time.deltaTime;

            // Get processed head tracking rotation (sensitivity, inversion, limits already applied)
            var (trackYaw, trackPitch, trackRoll) = OpenTrackReceiver.GetProcessedRotation(dt);

            if (TrackingModeState.IsRotationEnabled)
            {
                if (HeadTrackingConfig.CachedWorldSpaceYaw)
                {
                    ViewMatrixModifier.ApplyHeadRotationDecomposed(_camera, trackYaw, trackPitch, trackRoll);
                }
                else
                {
                    ViewMatrixModifier.ApplyHeadRotation(_camera, trackYaw, trackPitch, trackRoll);
                }
                _lastAppliedRotation = new Vector3(trackPitch, trackYaw, trackRoll);
            }
            else
            {
                ViewMatrixModifier.Reset(_camera);
                _lastAppliedRotation = Vector3.zero;
            }
            _wasTracking = true;

            if (TrackingModeState.IsPositionEnabled)
            {
                ApplyPositionOffset(trackYaw, trackPitch, trackRoll, dt);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyPositionOffset(float trackYaw, float trackPitch, float trackRoll, float dt)
        {
            Quat4 headRotQ = QuaternionUtils.FromYawPitchRoll(trackYaw, trackPitch, trackRoll);
            Vec3 posOffset = Position.Process(OpenTrackReceiver.GetLatestPosition(), headRotQ, dt);

            float offX = posOffset.X;
            float offY = Mathf.Clamp(posOffset.Y,
                -HeadTrackingConfig.CachedPositionLimitYDown,
                HeadTrackingConfig.CachedPositionLimitY);
            float offZ = posOffset.Z;

            Vector3 worldOffset =
                _camera.transform.right * offX +
                _camera.transform.up * offY +
                _camera.transform.forward * offZ;

            Matrix4x4 m = _camera.worldToCameraMatrix;
            m.m03 -= (m.m00 * worldOffset.x + m.m01 * worldOffset.y + m.m02 * worldOffset.z);
            m.m13 -= (m.m10 * worldOffset.x + m.m11 * worldOffset.y + m.m12 * worldOffset.z);
            m.m23 -= (m.m20 * worldOffset.x + m.m21 * worldOffset.y + m.m22 * worldOffset.z);
            _camera.worldToCameraMatrix = m;
        }

        private void OnDestroy()
        {
            if (_wasTracking && _camera != null)
            {
                ViewMatrixModifier.Reset(_camera);
            }
            ValheimHeadTrackingPlugin.Log.LogInfo("CameraTrackingHook destroyed");
        }

        /// <summary>
        /// Encapsulates Valheim-specific positional tracking state: processor, interpolator,
        /// and the enable toggle. Singleton because it must survive hook re-attachment across
        /// scene transitions.
        /// </summary>
        private sealed class PositionTracker
        {
            private readonly PositionProcessor _processor = new PositionProcessor
            {
                Settings = new PositionSettings(
                    sensitivityX: 2.0f, sensitivityY: 2.0f, sensitivityZ: 2.0f,
                    limitX: 0.60f, limitY: 0.60f, limitZ: 0.80f, limitZBack: 0.60f,
                    smoothing: 0.15f,
                    invertX: true, invertY: false, invertZ: true)
            };

            private readonly PositionInterpolator _interpolator = new PositionInterpolator();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vec3 Process(PositionData rawPos, Quat4 headRot, float dt)
            {
                var interpolated = _interpolator.Update(rawPos, dt);
                return _processor.Process(interpolated, headRot, dt);
            }

            public void Reset()
            {
                _processor.Reset();
                _interpolator.Reset();
            }

            public void Recenter()
            {
                _processor.SetCenter(OpenTrackReceiver.GetLatestPosition());
                _interpolator.Reset();
            }
        }
    }
}
