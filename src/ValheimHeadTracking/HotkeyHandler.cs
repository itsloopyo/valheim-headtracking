using System.Collections.Generic;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.State;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;
using ValheimHeadTracking.Config;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Fires the mod's hotkey actions from the key lists in CameraUnlock.ini. Every binding in a
    /// list is an ordinary item, the Ctrl+Shift chords included. Hotkeys are blocked during text
    /// input (chat, console, sign editing).
    /// </summary>
    public class HotkeyHandler : MonoBehaviour
    {
        private KeyBinding[] _toggle;
        private KeyBinding[] _cycleTrackingMode;
        private KeyBinding[] _yawMode;

        private void Start()
        {
            ValheimConfig config = HeadTrackingConfig.Current;
            _toggle = Parse("ToggleKey", config.ToggleKeyName);
            _cycleTrackingMode = Parse("CycleTrackingModeKey", config.CycleTrackingModeKeyName);
            _yawMode = Parse("YawModeKey", config.YawModeKeyName);
        }

        private void Update()
        {
            if (MessageHud.instance != null)
            {
                // A centre message replaces the one before it, so the config's messages go up as one.
                string pending = HeadTrackingConfig.TakePendingMessages();
                if (pending != null) ShowMessage(pending);
            }

            // Every binding fires on a key's down-edge, so a frame with none cannot trigger one.
            if (!Input.anyKeyDown) return;
            if (IsTextInputActive()) return;

            if (KeyBindingInput.IsTriggered(_toggle)) HandleToggle(TrackingState.Toggle());
            if (KeyBindingInput.IsTriggered(_cycleTrackingMode)) CycleTrackingMode();
            if (KeyBindingInput.IsTriggered(_yawMode)) ToggleYawMode();
        }

        private static void CycleTrackingMode()
        {
            TrackingMode mode = OpenTrackReceiver.Session.CycleMode();
            string desc = mode.Description();
            ShowMessage($"Tracking: {desc}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Tracking mode: {desc}");

            bool rotation;
            bool position;
            TrackingModeChannels.Encode(mode, out rotation, out position);
            HeadTrackingConfig.Save(c =>
            {
                c.RotationEnabled = rotation;
                c.PositionEnabled = position;
            });
        }

        private static void ToggleYawMode()
        {
            bool worldSpaceYaw = !HeadTrackingConfig.Current.WorldSpaceYaw;
            HeadTrackingConfig.Current.WorldSpaceYaw = worldSpaceYaw;
            string stateText = worldSpaceYaw ? "WORLD-LOCKED" : "CAMERA-LOCAL";
            ShowMessage($"Yaw Mode: {stateText}");
            ValheimHeadTrackingPlugin.Log.LogInfo($"Yaw mode toggled: {stateText}");

            HeadTrackingConfig.Save(c => c.WorldSpaceYaw = worldSpaceYaw);
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
        /// The master on/off. It changes this session only and never writes the file.
        /// </summary>
        private static void HandleToggle(bool newState)
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

        // The table's hotkey codec has read every list the file holds, so a list that does not
        // parse reaches here only from a legacy import the owner deferred: a .cfg key code Unity
        // names no key for, which the import writes as the number. The items that parse, the
        // chord among them, are bound and the rest are named in the log.
        private static KeyBinding[] Parse(string key, string text)
        {
            KeyBinding[] bindings;
            string error;
            if (KeyBindings.TryParse(text, out bindings, out error)) return bindings;

            var kept = new List<KeyBinding>();
            foreach (string item in text.Split(','))
            {
                if (KeyBindings.TryParse(item, out bindings, out error)) kept.AddRange(bindings);
                else ValheimHeadTrackingPlugin.Log.LogWarning("[Hotkeys] " + key + ": " + error + ", so it is not bound this session");
            }
            return kept.ToArray();
        }
    }
}
