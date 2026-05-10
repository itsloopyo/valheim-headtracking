using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimHeadTracking
{
    /// <summary>
    /// MonoBehaviour that offsets the crosshair and hover text to match head tracking.
    ///
    /// When head tracking rotates the camera away from the aim direction, the crosshair
    /// (normally at screen center) no longer shows where projectiles will go. This hook
    /// calculates the screen-space position where the aim direction intersects the screen
    /// and moves the crosshair and hover text there.
    ///
    /// Uses Camera.onPreCull callback to update AFTER head tracking is applied,
    /// ensuring all positions are synchronized with the camera rotation.
    /// </summary>
    public sealed class CrosshairOffsetHook : MonoBehaviour
    {
        // Cached GameCamera's Camera component to avoid GetComponent every frame
        private Camera _gameCameraComponent;
        private GameCamera _lastGameCameraInstance;

        // FieldInfo for Hud.m_hoverName. Looked up once in Awake; null result = not present
        // and we never retry (reflection is expensive).
        private static FieldInfo _hoverNameField;
        private static bool _hoverNameFieldResolved;

        private RectTransform _crosshairRect;
        private RectTransform _crosshairBowRect;
        private RectTransform _hoverNameRect;
        private Canvas _hudCanvas;

        // Original positions to restore when decoupling is disabled
        private Vector2 _originalCrosshairPosition;
        private Vector2 _originalBowCrosshairPosition;
        private Vector2 _originalHoverNamePosition;
        private bool _positionsCaptured;

        // Track decoupling state
        private bool _wasDecoupled;

        private void OnEnable()
        {
            Camera.onPreCull += OnCameraPreCull;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= OnCameraPreCull;
        }

        /// <summary>
        /// Called just before each camera renders. We update the crosshair here
        /// to ensure it's synchronized with the head tracking applied in OnPreCull.
        /// </summary>
        private void OnCameraPreCull(Camera cam)
        {
            // Camera.onPreCull fires for every camera that renders this frame (shadow cascades,
            // reflection probes, UI cameras, …). For non-game-camera invocations we want to
            // exit on the cheapest possible test. If the cached game-camera reference still
            // matches what's about to render, that's the only Unity == we have to pay; we skip
            // the GameCamera.instance native-transition lookup entirely on the common path.
            Camera cached = _gameCameraComponent;
            if (cached != null)
            {
                if (cam != cached) return;
            }
            else
            {
                GameCamera gameCamera = GameCamera.instance;
                if (gameCamera == null) return;

                if (_lastGameCameraInstance != gameCamera)
                {
                    _gameCameraComponent = gameCamera.GetComponent<Camera>();
                    _lastGameCameraInstance = gameCamera;
                }
                if (cam != _gameCameraComponent) return;
            }

            if (!EnsureCrosshairReferences())
            {
                return;
            }

            bool isDecoupled = AimState.IsDecoupled() && HeadTrackingConfig.CachedShowDecoupledCrosshair;

            if (isDecoupled)
            {
                ApplyCrosshairOffset(cam);
            }
            else if (_wasDecoupled)
            {
                RestoreOriginalPositions();
            }

            _wasDecoupled = isDecoupled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EnsureCrosshairReferences()
        {
            Hud hud = Hud.instance;
            if (hud == null)
            {
                return false;
            }

            if (_crosshairRect == null && !AcquireCrosshair(hud))
            {
                return false;
            }

            if (_crosshairBowRect == null && hud.m_crosshairBow != null)
            {
                _crosshairBowRect = hud.m_crosshairBow.rectTransform;
            }

            if (_hoverNameRect == null)
            {
                _hoverNameRect = ResolveHoverNameRect(hud);
            }

            if (!_positionsCaptured)
            {
                CaptureOriginalPositions();
            }

            return true;
        }

        // Acquires the main crosshair from a (possibly fresh) Hud. Hud is rebuilt across scene
        // transitions, so sibling refs and the snapshot flag must be invalidated whenever we
        // re-bind to a new instance — otherwise we'd offset against a stale layout.
        private bool AcquireCrosshair(Hud hud)
        {
            Image crosshair = hud.m_crosshair;
            if (crosshair == null)
            {
                return false;
            }

            _crosshairRect = crosshair.rectTransform;
            _hudCanvas = crosshair.canvas;
            _crosshairBowRect = null;
            _hoverNameRect = null;
            _positionsCaptured = false;
            return true;
        }

        private void CaptureOriginalPositions()
        {
            _originalCrosshairPosition = _crosshairRect.anchoredPosition;
            if (_crosshairBowRect != null)
            {
                _originalBowCrosshairPosition = _crosshairBowRect.anchoredPosition;
            }
            if (_hoverNameRect != null)
            {
                _originalHoverNamePosition = _hoverNameRect.anchoredPosition;
            }
            _positionsCaptured = true;

            ValheimHeadTrackingPlugin.Log.LogInfo(
                $"CrosshairOffsetHook: Captured original positions - " +
                $"Crosshair: {_originalCrosshairPosition}, Bow: {_originalBowCrosshairPosition}, HoverName: {_originalHoverNamePosition}");
        }

        // Reflection avoids a direct TMPro reference. Field lookup is one-shot across all
        // instances; null result is sticky (the field genuinely doesn't exist on this build).
        private static RectTransform ResolveHoverNameRect(Hud hud)
        {
            if (!_hoverNameFieldResolved)
            {
                _hoverNameField = typeof(Hud).GetField(
                    "m_hoverName", BindingFlags.Public | BindingFlags.Instance);
                _hoverNameFieldResolved = true;
            }

            if (_hoverNameField == null)
            {
                return null;
            }

            return _hoverNameField.GetValue(hud) is Component hoverComponent
                ? hoverComponent.GetComponent<RectTransform>()
                : null;
        }

        /// <summary>
        /// Calculates and applies the crosshair offset based on tracking rotation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyCrosshairOffset(Camera cam)
        {
            // Single Canvas.scaleFactor native call (was two via the ternary's reread).
            float canvasScale = 1f;
            if (_hudCanvas != null)
            {
                float sf = _hudCanvas.scaleFactor;
                if (sf > 0f) canvasScale = sf;
            }

            Vector2 offset = AimState.CalculateCrosshairOffset(cam, canvasScale);

            if (_crosshairRect != null)
            {
                _crosshairRect.anchoredPosition = _originalCrosshairPosition + offset;
            }

            if (_crosshairBowRect != null && _crosshairBowRect.gameObject.activeSelf)
            {
                _crosshairBowRect.anchoredPosition = _originalBowCrosshairPosition + offset;
            }

            if (_hoverNameRect != null && _hoverNameRect.gameObject.activeSelf)
            {
                _hoverNameRect.anchoredPosition = _originalHoverNamePosition + offset;
            }
        }

        /// <summary>
        /// Restores the crosshair to its original position when decoupling is disabled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RestoreOriginalPositions()
        {
            if (!_positionsCaptured)
            {
                return;
            }

            if (_crosshairRect != null)
            {
                _crosshairRect.anchoredPosition = _originalCrosshairPosition;
            }

            if (_crosshairBowRect != null)
            {
                _crosshairBowRect.anchoredPosition = _originalBowCrosshairPosition;
            }

            if (_hoverNameRect != null)
            {
                _hoverNameRect.anchoredPosition = _originalHoverNamePosition;
            }
        }

        private void OnDestroy()
        {
            // Attempt to restore original positions on cleanup
            RestoreOriginalPositions();
            ValheimHeadTrackingPlugin.Log.LogInfo("CrosshairOffsetHook destroyed");
        }
    }
}
