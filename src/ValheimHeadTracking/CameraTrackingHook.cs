using System.Runtime.CompilerServices;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.State;
using CameraUnlock.Core.Tracking;
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

        /// <summary>
        /// Gets the last applied tracking rotation (yaw, pitch, roll in degrees).
        /// Useful for aim decoupling calculations.
        /// </summary>
        public Vector3 LastAppliedRotation => _lastAppliedRotation;

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

            // Session.Update keeps the pipeline fed every frame (auto-recenter, smoothing,
            // hold-on-loss). False only when no tracker data has ever arrived.
            HeadTrackingSession session = OpenTrackReceiver.Session;
            if (session == null || !session.Update(Time.deltaTime))
            {
                ResetIfTracking();
                return;
            }

            if (!GameStateDetector.ShouldTrack())
            {
                ResetIfTracking();
                return;
            }

            ApplyTracking(session);
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
                _lastAppliedRotation = Vector3.zero;
            }
        }

        /// <summary>
        /// Applies the session's processed rotation and position offset to the camera's
        /// view matrix. Branches on HeadTrackingConfig.CachedWorldSpaceYaw:
        ///  - true (default): ApplyHeadRotationDecomposed, yaw around world up then local pitch/roll.
        ///  - false: ApplyHeadRotation, single camera-local YXZ quaternion (leans at extreme pitch).
        /// Camera.transform is never modified in either mode; aim decoupling still works.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyTracking(HeadTrackingSession session)
        {
            float yaw = 0f, pitch = 0f, roll = 0f;
            if (session.RotationActive)
            {
                TrackingPose rotation = session.Rotation;
                yaw = rotation.Yaw;
                pitch = rotation.Pitch;
                roll = rotation.Roll;
            }

            Vec3 posOffset = session.PositionOffset;
            Vector3 offset = new Vector3(posOffset.X, posOffset.Y, posOffset.Z);

            if (HeadTrackingConfig.CachedWorldSpaceYaw)
            {
                ViewMatrixModifier.ApplyHeadRotationDecomposed(_camera, yaw, pitch, roll, offset);
            }
            else
            {
                ViewMatrixModifier.ApplyHeadRotation(_camera, yaw, pitch, roll, offset);
            }

            _lastAppliedRotation = new Vector3(pitch, yaw, roll);
            _wasTracking = true;
        }

        private void OnDestroy()
        {
            if (_wasTracking && _camera != null)
            {
                ViewMatrixModifier.Reset(_camera);
            }
            ValheimHeadTrackingPlugin.Log.LogInfo("CameraTrackingHook destroyed");
        }
    }
}
