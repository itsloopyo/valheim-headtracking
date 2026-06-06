using System.Runtime.CompilerServices;
using CameraUnlock.Core.State;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Manages the decoupled aim state, tracking where the player is actually aiming
    /// independent of head tracking rotation.
    ///
    /// The game's m_eye.forward represents the "true" aim direction based on mouse input.
    /// When head tracking is applied to the camera, we need to preserve this original
    /// aim direction for projectiles and attacks, while allowing the camera to look elsewhere.
    ///
    /// Uses shared CanvasCompensation utilities from cameraunlock-core.
    /// </summary>
    public static class AimState
    {
        private const float MaxRaycastDistance = 1000f;
        private const float MinRaycastDistance = 0.5f;
        private const float DistanceSmoothingRate = 15f;
        // 30 Hz is above the effective Nyquist of the 15 Hz exponential smoother that consumes
        // the distance, so rate-limiting the raycast here is indistinguishable from per-frame
        // raycasting at any display refresh >= 30 fps, while cutting raycast cost by 50-80% at
        // 60-144 fps. Valheim raycasts against terrain + buildings over 1000 m are the dominant
        // cost on the head-tracking per-frame path.
        private const float RaycastInterval = 1f / 30f;
        private static float _lastHitDistance = 100f;
        private static float _raycastAccumulator;

        // Cached CameraController reference. CameraController is added in plugin Awake() and
        // lives for the plugin's lifetime, so once located it never needs re-resolution.
        private static CameraController _cachedController;
        // Direct hook cache. The hook is recreated on scene transitions; Unity's overloaded ==
        // returns true-for-null on a destroyed Object so a `_cachedHook != null` test recovers
        // automatically by falling through to the controller chain.
        private static CameraTrackingHook _cachedHook;

        /// <summary>
        /// Gets the current aim direction in world space.
        /// This is the direction projectiles should travel, independent of head tracking.
        /// Returns the player's m_eye.forward which represents mouse-controlled aim.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetAimDirection()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return Vector3.forward;
            }

            // m_eye.forward is the game's intended aim direction
            // We modify only the camera's view matrix, not m_eye, so this remains accurate
            Transform eye = player.m_eye;
            if (eye == null)
            {
                return Vector3.forward;
            }

            return eye.forward;
        }

        /// <summary>
        /// Gets the current aim direction as a quaternion.
        /// Useful for setting projectile rotation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion GetAimRotation()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return Quaternion.identity;
            }

            Transform eye = player.m_eye;
            if (eye == null)
            {
                return Quaternion.identity;
            }

            return eye.rotation;
        }

        /// <summary>
        /// Gets the tracking offset that was applied to the camera.
        /// Returns (pitch, yaw, roll) in degrees.
        /// This represents how much the camera has been rotated away from the aim direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetTrackingOffset()
        {
            CameraTrackingHook hook = GetCurrentHook();
            if (hook == null)
            {
                return Vector3.zero;
            }

            return hook.LastAppliedRotation;
        }

        /// <summary>
        /// Returns true if aim decoupling is currently active.
        /// Decoupling is active when:
        /// - Feature is enabled in config
        /// - Head tracking is enabled
        /// - A non-zero tracking offset is being applied (including the held pose during
        ///   tracking loss - the hook zeroes its rotation when it stops applying tracking)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDecoupled()
        {
            if (!HeadTrackingConfig.CachedEnableAimDecoupling)
            {
                return false;
            }

            if (!TrackingState.IsEnabled)
            {
                return false;
            }

            CameraTrackingHook hook = GetCurrentHook();
            if (hook == null)
            {
                return false;
            }

            // Read the rotation directly off the hook to skip the Vector3 round-trip via
            // GetTrackingOffset(), which would otherwise return Vector3.zero for the null-hook
            // branch — already handled above.
            Vector3 offset = hook.LastAppliedRotation;
            const float threshold = 0.1f;
            return Mathf.Abs(offset.x) > threshold ||
                   Mathf.Abs(offset.y) > threshold;
        }

        /// <summary>
        /// Calculates the screen-space offset for the crosshair based on tracking rotation.
        /// Returns the offset in UI coordinates (pixels, adjusted for canvas scale).
        ///
        /// Uses shared CanvasCompensation.CalculateAimScreenOffset from cameraunlock-core,
        /// which uses Unity's camera.WorldToScreenPoint() to correctly handle all rotation
        /// combinations.
        /// </summary>
        /// <param name="camera">The game camera</param>
        /// <param name="canvasScale">The UI canvas scale factor</param>
        /// <returns>Offset to apply to crosshair anchoredPosition</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 CalculateCrosshairOffset(Camera camera, float canvasScale)
        {
            if (camera == null || canvasScale <= 0f)
            {
                return Vector2.zero;
            }

            // Get the actual aim direction from m_eye.forward
            // This is where projectiles/attacks go - the game's true aim, unaffected by head tracking
            Vector3 aimDirection = GetAimDirection();

            // Amortize the 1 km physics raycast over 30 Hz. The smoother downstream is the same
            // `1 - exp(-rate * dt)` exponential, so feeding it the accumulated dt when we do fire
            // yields the same closed-form convergence as per-frame sampling (exp(a+b) = exp(a)*exp(b)).
            _raycastAccumulator += Time.deltaTime;
            if (_raycastAccumulator >= RaycastInterval)
            {
                float accumDt = _raycastAccumulator;
                _raycastAccumulator = 0f;

                RaycastHit hit;
                if (Physics.Raycast(camera.transform.position, aimDirection, out hit, MaxRaycastDistance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.distance >= MinRaycastDistance)
                {
                    float t = 1f - Mathf.Exp(-DistanceSmoothingRate * accumDt);
                    _lastHitDistance = Mathf.Lerp(_lastHitDistance, hit.distance, t);
                }
            }

            return CanvasCompensation.CalculateAimScreenOffset(camera, aimDirection, _lastHitDistance, canvasScale);
        }

        /// <summary>
        /// Helper to get the current camera tracking hook.
        /// Caches the CameraController once (it never changes after plugin init) and the hook
        /// itself; on the hot path this is a single Unity null check + return.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CameraTrackingHook GetCurrentHook()
        {
            CameraTrackingHook hook = _cachedHook;
            if (hook != null)
            {
                return hook;
            }

            var plugin = ValheimHeadTrackingPlugin.Instance;
            if (plugin == null)
            {
                _cachedController = null;
                return null;
            }

            if (_cachedController == null)
            {
                _cachedController = plugin.GetComponent<CameraController>();
            }

            return _cachedHook = _cachedController?.CurrentHook;
        }
    }
}
