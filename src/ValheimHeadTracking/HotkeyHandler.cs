using System;
using CameraUnlock.Core.State;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Handles hotkey input for head tracking.
    /// Polls the nav-cluster keys (toggle, cycle mode, reticle, yaw-mode) plus the
    /// shared Ctrl+Shift+letter chord bindings. Hotkeys are blocked during text
    /// input (chat, console, sign editing).
    /// </summary>
    public class HotkeyHandler : MonoBehaviour
    {
        private NavKeyBinding _toggleBinding;
        private NavKeyBinding _cycleModeBinding;
        private NavKeyBinding _reticleBinding;
        private NavKeyBinding _yawModeBinding;

        private void Start()
        {
            var config = HeadTrackingConfig.Current;
            _toggleBinding = new NavKeyBinding(config.ToggleKey, () => HandleToggle(TrackingState.Toggle()));
            _cycleModeBinding = new NavKeyBinding(config.PositionToggleKey, CycleTrackingMode);
            _reticleBinding = new NavKeyBinding(config.ReticleToggleKey, ToggleReticle);
            _yawModeBinding = new NavKeyBinding(config.YawModeKey, ToggleYawMode);
        }

        private void Update()
        {
            if (IsTextInputActive()) return;

            // Chord bindings: Ctrl+Shift+<letter> from the shared Y/G/H/U cluster,
            // so keyboards without a nav cluster still work.
            if (ChordHotkeys.IsPressed(ChordHotkeys.ToggleLetter)) HandleToggle(TrackingState.Toggle());
            if (ChordHotkeys.IsPressed(ChordHotkeys.PositionLetter)) CycleTrackingMode();
            if (ChordHotkeys.IsPressed(ChordHotkeys.FourthToggleLetter)) ToggleYawMode();
            if (ChordHotkeys.IsPressed(ChordHotkeys.FifthToggleLetter)) ToggleReticle();

            _toggleBinding.Poll();
            _cycleModeBinding.Poll();
            _reticleBinding.Poll();
            _yawModeBinding.Poll();
        }

        private void CycleTrackingMode()
        {
            HeadTrackingSession session = OpenTrackReceiver.Session;
            if (session == null) return;

            TrackingMode mode = session.CycleMode();
            string desc = mode.Description();
            ShowMessage($"Tracking: {desc}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Tracking mode: {desc}");
        }

        private void ToggleReticle()
        {
            bool newValue = !HeadTrackingConfig.Current.ShowDecoupledCrosshair;
            HeadTrackingConfig.SetShowDecoupledCrosshair(newValue);
            string stateText = newValue ? "ON" : "OFF";
            ShowMessage($"Aim Reticle: {stateText}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Aim reticle toggled: {stateText}");
        }

        private void ToggleYawMode()
        {
            bool newValue = !HeadTrackingConfig.Current.WorldSpaceYaw;
            HeadTrackingConfig.SetWorldSpaceYaw(newValue);
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
        /// Handles the toggle hotkey press - shows a message with the new state.
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
        /// Encapsulates the poll-and-fire-on-edge pattern for a single nav-cluster key.
        /// </summary>
        private sealed class NavKeyBinding
        {
            private readonly Action _onPressed;
            private readonly KeyCode _key;
            private bool _wasPressed;

            public NavKeyBinding(KeyCode key, Action onPressed)
            {
                _onPressed = onPressed;
                _key = key;
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
