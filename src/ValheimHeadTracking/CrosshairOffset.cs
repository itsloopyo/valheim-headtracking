using System;
using System.Reflection;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Moves the game's crosshair, bow crosshair and hover text to where the clean camera's
    /// centre ray lands in the head-tracked view. CameraTrackingHook calls it right after it
    /// writes the view matrix, so the projection always goes through this frame's matrix.
    /// </summary>
    internal static class CrosshairOffset
    {
        // Player.FindHoverObject casts from GameCamera's transform along its forward with this
        // mask and skips the local player's own body. That ray is what the crosshair marks: its
        // colour and the hover text both come from it.
        private static readonly string[] InteractLayers =
        {
            "item", "piece", "piece_nonsolid", "Default", "static_solid", "Default_small",
            "character", "character_net", "terrain", "vehicle", "character_ghost",
        };

        private const float MaxAimDistance = 1000f;

        // Past the surface where the ray entered the player's collider. A ray that starts
        // inside a collider does not report it, so one step clears the body.
        private const float SelfHitStep = 0.01f;

        private static int _interactMask;
        private static bool _interactMaskResolved;

        private static FieldInfo _hoverNameField;
        private static bool _hoverNameFieldResolved;

        private static RectTransform _crosshairRect;
        private static RectTransform _crosshairBowRect;
        private static RectTransform _hoverNameRect;
        private static Canvas _hudCanvas;

        private static Vector2 _originalCrosshairPosition;
        private static Vector2 _originalBowCrosshairPosition;
        private static Vector2 _originalHoverNamePosition;

        private static bool _moved;

        public static void Follow(Camera cam)
        {
            if (!EnsureReferences()) return;

            float canvasScale = 1f;
            if (_hudCanvas != null)
            {
                float sf = _hudCanvas.scaleFactor;
                if (sf > 0f) canvasScale = sf;
            }

            Transform camTransform = cam.transform;
            Vector3 origin = camTransform.position;
            Vector3 aim = camTransform.forward;

            Vector3 hitPoint;
            Vector2 offset = TryFindAimPoint(origin, aim, out hitPoint)
                ? CanvasCompensation.CalculateScreenOffsetFromWorldPoint(cam, hitPoint, canvasScale)
                : CanvasCompensation.CalculateAimScreenOffset(cam, aim, MaxAimDistance, canvasScale);

            _crosshairRect.anchoredPosition = _originalCrosshairPosition + offset;
            if (_crosshairBowRect != null)
            {
                _crosshairBowRect.anchoredPosition = _originalBowCrosshairPosition + offset;
            }
            if (_hoverNameRect != null)
            {
                _hoverNameRect.anchoredPosition = _originalHoverNamePosition + offset;
            }
            _moved = true;
        }

        public static void Restore()
        {
            if (!_moved) return;
            _moved = false;

            // A Hud torn down by a scene change destroys these with it, and Unity's == then
            // reports them null, so there is nothing left to put back.
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

        // Nearest hit along the clean aim ray, passing through the local player's own colliders
        // as FindHoverObject does. RaycastHit.transform is the attached rigidbody's transform
        // when there is one, which is the object the game compares against the player.
        private static bool TryFindAimPoint(Vector3 origin, Vector3 direction, out Vector3 point)
        {
            Player player = Player.m_localPlayer;
            Transform self = player != null ? player.transform : null;
            int mask = InteractMask();

            float remaining = MaxAimDistance;
            RaycastHit hit;
            while (Physics.Raycast(origin, direction, out hit, remaining, mask, QueryTriggerInteraction.Ignore))
            {
                if (self == null || hit.transform != self)
                {
                    point = hit.point;
                    return true;
                }

                float advance = hit.distance + SelfHitStep;
                origin += direction * advance;
                remaining -= advance;
                if (remaining <= 0f) break;
            }

            point = default(Vector3);
            return false;
        }

        private static int InteractMask()
        {
            if (_interactMaskResolved) return _interactMask;

            int mask = 0;
            foreach (string name in InteractLayers)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer < 0)
                {
                    throw new InvalidOperationException(
                        "Valheim has no layer named '" + name + "', which Player.FindHoverObject casts against.");
                }
                mask |= 1 << layer;
            }

            _interactMask = mask;
            _interactMaskResolved = true;
            return mask;
        }

        private static bool EnsureReferences()
        {
            if (_crosshairRect != null) return true;

            // The Hud is rebuilt on every scene change, taking the old references with it.
            Hud hud = Hud.instance;
            if (hud == null) return false;

            Image crosshair = hud.m_crosshair;
            if (crosshair == null) return false;

            _crosshairRect = crosshair.rectTransform;
            _hudCanvas = crosshair.canvas;
            _crosshairBowRect = hud.m_crosshairBow != null ? hud.m_crosshairBow.rectTransform : null;
            _hoverNameRect = ResolveHoverNameRect(hud);

            _originalCrosshairPosition = _crosshairRect.anchoredPosition;
            if (_crosshairBowRect != null)
            {
                _originalBowCrosshairPosition = _crosshairBowRect.anchoredPosition;
            }
            if (_hoverNameRect != null)
            {
                _originalHoverNamePosition = _hoverNameRect.anchoredPosition;
            }
            _moved = false;

            ValheimHeadTrackingPlugin.Log.LogInfo(
                $"Crosshair: captured original positions - " +
                $"Crosshair: {_originalCrosshairPosition}, Bow: {_originalBowCrosshairPosition}, HoverName: {_originalHoverNamePosition}");
            return true;
        }

        // Reflection avoids a direct TMPro reference.
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
    }
}
