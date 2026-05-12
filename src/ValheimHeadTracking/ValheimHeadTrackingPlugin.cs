using BepInEx;
using BepInEx.Logging;
using CameraUnlock.Core.State;

namespace ValheimHeadTracking
{
    /// <summary>
    /// Main BepInEx plugin entry point for Valheim Head Tracking.
    /// Initializes all subsystems and attaches MonoBehaviour components.
    ///
    /// Architecture:
    /// - OpenTrackReceiver: Receives UDP packets from OpenTrack (background thread)
    /// - CameraController: Watches for GameCamera and attaches CameraTrackingHook
    /// - CameraTrackingHook: Applies head tracking via view matrix in OnPreCull
    /// - HotkeyHandler: Handles toggle (End) and recenter (Home) hotkeys
    /// - CrosshairOffsetHook: Moves crosshair to show actual aim position
    /// </summary>
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class ValheimHeadTrackingPlugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.cameraunlock.valheim.headtracking";
        public const string PLUGIN_NAME = "Valheim Head Tracking";
        public const string PLUGIN_VERSION = "0.1.3";

        internal static ManualLogSource Log;

        /// <summary>
        /// Singleton instance for accessing plugin components.
        /// </summary>
        public static ValheimHeadTrackingPlugin Instance { get; private set; }

        private CameraController _cameraController;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loading...");

            // Initialize configuration
            HeadTrackingConfig.Initialize(Config);
            Log.LogInfo("Configuration loaded");

            // Initialize state management
            TrackingState.Initialize(HeadTrackingConfig.EnableOnStartup.Value);
            Log.LogInfo($"Head tracking enabled: {TrackingState.IsEnabled}");

            // Initialize UDP receiver (retries automatically if port is in use)
            OpenTrackReceiver.Start(HeadTrackingConfig.UdpPort.Value);

            // Attach camera controller (manages CameraTrackingHook attachment)
            _cameraController = gameObject.AddComponent<CameraController>();
            Log.LogInfo("Camera controller initialized");

            // Attach hotkey handler
            gameObject.AddComponent<HotkeyHandler>();
            Log.LogInfo("Hotkey handler initialized");

            // Attach crosshair offset hook
            gameObject.AddComponent<CrosshairOffsetHook>();
            Log.LogInfo("Crosshair offset hook initialized");

            Log.LogInfo($"{PLUGIN_NAME} loaded successfully!");
        }

        private void OnDestroy()
        {
            Log.LogInfo("Cleaning up...");

            OpenTrackReceiver.Stop();
            Log.LogInfo("UDP receiver stopped");

            Instance = null;
            Log.LogInfo("Cleanup complete");
        }
    }
}
