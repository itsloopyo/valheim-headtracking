using System;
using BepInEx.Configuration;
using CameraUnlock.Core.State;
using CameraUnlock.Core.Unity.BepInEx.Input;
using UnityEngine;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Handles hotkey input for toggling head tracking and recentering.
    /// Uses the shared BepInExHotkeyHandler for recenter/toggle, and polls additional
    /// nav-cluster keys (position, reticle, yaw-mode) plus Ctrl+Shift+letter chord
    /// fallbacks. Hotkeys are blocked during text input (chat, console, sign editing).
    /// </summary>
    public class HotkeyHandler : MonoBehaviour
    {
        private BepInExHotkeyHandler _handler;

        private NavKeyBinding _positionBinding;
        private NavKeyBinding _reticleBinding;
        private NavKeyBinding _yawModeBinding;

        private void Start()
        {
            _handler = gameObject.AddComponent<BepInExHotkeyHandler>();
            _handler.Initialize(HeadTrackingConfig.RecenterKey, HeadTrackingConfig.ToggleKey);
            _handler.IsInputBlocked = IsTextInputActive;
            _handler.OnRecenter += HandleRecenter;
            _handler.OnToggle += HandleToggle;

            _positionBinding = new NavKeyBinding(HeadTrackingConfig.PositionToggleKey, TogglePosition);
            _reticleBinding = new NavKeyBinding(HeadTrackingConfig.ReticleToggleKey, ToggleReticle);
            _yawModeBinding = new NavKeyBinding(HeadTrackingConfig.YawModeKey, ToggleYawMode);
        }

        private void Update()
        {
            if (IsTextInputActive()) return;

            // Chord bindings: Ctrl+Shift+<letter>. Registered unconditionally alongside the
            // user-configurable nav-cluster keys so keyboards without a nav cluster still work.
            if (IsCtrlShiftHeld())
            {
                if (Input.GetKeyDown(KeyCode.T)) HandleRecenter();
                if (Input.GetKeyDown(KeyCode.Y)) HandleToggle(TrackingState.Toggle());
                if (Input.GetKeyDown(KeyCode.G)) TogglePosition();
                if (Input.GetKeyDown(KeyCode.H)) ToggleYawMode();
                if (Input.GetKeyDown(KeyCode.U)) ToggleReticle();
            }

            _positionBinding.Poll();
            _reticleBinding.Poll();
            _yawModeBinding.Poll();
        }

        private static bool IsCtrlShiftHeld() =>
            (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        private void TogglePosition()
        {
            CameraTrackingHook.PositionEnabled = !CameraTrackingHook.PositionEnabled;
            if (!CameraTrackingHook.PositionEnabled)
            {
                CameraTrackingHook.ResetPosition();
            }
            string stateText = CameraTrackingHook.PositionEnabled ? "ON" : "OFF";
            ShowMessage($"Position Tracking: {stateText}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Position tracking toggled: {stateText}");
        }

        private void ToggleReticle()
        {
            bool newValue = !HeadTrackingConfig.ShowDecoupledCrosshair.Value;
            HeadTrackingConfig.ShowDecoupledCrosshair.Value = newValue;
            string stateText = newValue ? "ON" : "OFF";
            ShowMessage($"Aim Reticle: {stateText}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Aim reticle toggled: {stateText}");
        }

        private void ToggleYawMode()
        {
            bool newValue = !HeadTrackingConfig.WorldSpaceYaw.Value;
            HeadTrackingConfig.WorldSpaceYaw.Value = newValue;
            string stateText = newValue ? "WORLD-LOCKED" : "CAMERA-LOCAL";
            ShowMessage($"Yaw Mode: {stateText}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Yaw mode toggled: {stateText}");
        }

        /// <summary>
        /// Checks if text input is currently active (chat, console, sign editing).
        /// Returns true if hotkeys should be blocked.
        /// </summary>
        private static bool IsTextInputActive()
        {
            return TextInput.IsVisible() || Console.IsVisible();
        }

        /// <summary>
        /// Handles the recenter hotkey press.
        /// Sets the current head position as the center reference point.
        /// </summary>
        private void HandleRecenter()
        {
            if (!OpenTrackReceiver.IsReceiving)
            {
                ShowMessage("Head Tracking: No signal from OpenTrack");
                ValheimHeadTrackingPlugin.Log.LogWarning("Recenter failed: No OpenTrack signal");
                return;
            }

            OpenTrackReceiver.Recenter();
            CameraTrackingHook.RecenterPosition();
            ShowMessage("Head Tracking: Recentered");
            ValheimHeadTrackingPlugin.Log.LogInfo("Head tracking recentered");
        }

        /// <summary>
        /// Handles the toggle hotkey press — shows a message with the new state.
        /// </summary>
        private void HandleToggle(bool newState)
        {
            string stateText = newState ? "ON" : "OFF";
            ShowMessage($"Head Tracking: {stateText}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Head tracking toggled: {stateText}");
        }

        /// <summary>
        /// Shows a message to the player using Valheim's MessageHud.
        /// Logs to console if MessageHud is not available (not in game world).
        /// </summary>
        private static void ShowMessage(string text)
        {
            if (MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
            }
            else
            {
                ValheimHeadTrackingPlugin.Log.LogInfo($"[HUD unavailable] {text}");
            }
        }

        /// <summary>
        /// Encapsulates the poll-and-fire-on-edge pattern for a single nav-cluster key,
        /// with its KeyCode value cached from the backing ConfigEntry and auto-refreshed
        /// on SettingChanged (avoids a dictionary lookup per frame).
        /// </summary>
        private sealed class NavKeyBinding
        {
            private readonly Action _onPressed;
            private KeyCode _key;
            private bool _wasPressed;

            public NavKeyBinding(ConfigEntry<KeyCode> entry, Action onPressed)
            {
                _onPressed = onPressed;
                _key = entry.Value;
                entry.SettingChanged += (_, __) => _key = entry.Value;
            }

            public void Poll()
            {
                if (_key == KeyCode.None) return;

                bool isPressed = Input.GetKey(_key);
                if (isPressed && !_wasPressed)
                {
                    _onPressed();
                }
                _wasPressed = isPressed;
            }
        }
    }
}
