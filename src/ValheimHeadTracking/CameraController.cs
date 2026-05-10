using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Manages the CameraTrackingHook attachment to Valheim's GameCamera.
    /// Extends the shared CameraLifecycleManager base class.
    /// </summary>
    public sealed class CameraController : CameraLifecycleManager
    {
        // Memoize GetComponent<Camera>() against the current GameCamera instance.
        // Base class calls FindCamera() every LateUpdate; GetComponent<T> is a managed→native
        // transition (~100-500 ns) that's wasted when the GameCamera hasn't changed.
        private GameCamera _cachedGameCamera;
        private Camera _cachedCamera;

        /// <summary>
        /// Finds Valheim's GameCamera and returns its Camera component.
        /// </summary>
        protected override Camera FindCamera()
        {
            GameCamera gameCamera = GameCamera.instance;
            if (gameCamera == null)
            {
                _cachedGameCamera = null;
                _cachedCamera = null;
                return null;
            }
            if (_cachedGameCamera != gameCamera)
            {
                _cachedGameCamera = gameCamera;
                _cachedCamera = gameCamera.GetComponent<Camera>();
            }
            return _cachedCamera;
        }

        /// <summary>
        /// Creates the CameraTrackingHook on the camera GameObject.
        /// </summary>
        protected override Component CreateTrackingHook(Camera camera)
        {
            return camera.gameObject.AddComponent<CameraTrackingHook>();
        }

        /// <summary>
        /// Finds any existing CameraTrackingHook on the camera (from previous attachment).
        /// </summary>
        protected override Component FindExistingHook(Camera camera)
        {
            return camera.gameObject.GetComponent<CameraTrackingHook>();
        }

        /// <summary>
        /// Logs when camera is found and hook is attached.
        /// </summary>
        protected override void OnCameraFound(Camera camera, Component hook, bool isReused)
        {
            string message = isReused
                ? "CameraController: Reusing existing CameraTrackingHook on GameCamera"
                : "CameraController: Attached CameraTrackingHook to GameCamera";
            ValheimHeadTrackingPlugin.Log.LogInfo(message);
        }

        /// <summary>
        /// Logs when camera is lost (scene transition, etc.).
        /// </summary>
        protected override void OnCameraLost()
        {
            ValheimHeadTrackingPlugin.Log.LogInfo("CameraController: GameCamera lost (scene transition?)");
        }

        /// <summary>
        /// Gets the currently attached hook, if any.
        /// Useful for accessing tracking state (e.g., for aim decoupling).
        /// </summary>
        public CameraTrackingHook CurrentHook => TrackingHook as CameraTrackingHook;

        /// <summary>
        /// Logs cleanup on destroy.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            ValheimHeadTrackingPlugin.Log.LogInfo("CameraController destroyed");
        }
    }
}
