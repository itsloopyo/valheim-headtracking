using System;
using System.Reflection;
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

        // MessageHud.ShowMessage gained a trailing optional parameter in the 2026-09-26 update, so
        // a direct call binds to one arity and throws MissingMethodException on the other build.
        private static MethodInfo _showMessage;
        private static object[] _showMessageDefaults;

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
            MessageHud hud = MessageHud.instance;
            if (hud == null)
            {
                ValheimHeadTrackingPlugin.Log.LogInfo($"[HUD unavailable] {text}");
                return;
            }

            if (_showMessage == null) ResolveShowMessage();

            object[] args = (object[])_showMessageDefaults.Clone();
            args[0] = MessageHud.MessageType.Center;
            args[1] = text;
            _showMessage.Invoke(hud, args);
        }

        private static void ResolveShowMessage()
        {
            foreach (MethodInfo method in typeof(MessageHud).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name != "ShowMessage") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2
                    || parameters[0].ParameterType != typeof(MessageHud.MessageType)
                    || parameters[1].ParameterType != typeof(string))
                {
                    continue;
                }

                var defaults = new object[parameters.Length];
                for (int i = 2; i < parameters.Length; i++)
                {
                    if (!parameters[i].IsOptional)
                    {
                        throw new MissingMethodException(
                            "MessageHud.ShowMessage parameter '" + parameters[i].Name + "' has no default");
                    }
                    defaults[i] = parameters[i].DefaultValue;
                }

                _showMessageDefaults = defaults;
                _showMessage = method;
                return;
            }

            throw new MissingMethodException("MessageHud", "ShowMessage(MessageType, string, ...)");
        }

        // The table's hotkey codec has read every list the file holds, and the legacy import
        // writes only key lists, so a list that does not parse is a bug.
        private static KeyBinding[] Parse(string key, string text)
        {
            KeyBinding[] bindings;
            string error;
            if (!KeyBindings.TryParse(text, out bindings, out error))
                throw new InvalidOperationException("[Hotkeys] " + key + "=" + text + ": " + error);
            return bindings;
        }
    }
}
