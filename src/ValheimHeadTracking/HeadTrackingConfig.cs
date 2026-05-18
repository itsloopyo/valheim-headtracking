using System;
using BepInEx.Configuration;
using CameraUnlock.Core.Unity.BepInEx.Config;
using UnityEngine;

namespace ValheimHeadTracking
{
    /// <summary>
    /// BepInEx configuration for Valheim head tracking.
    /// Extends HeadTrackingConfigBase with Valheim-specific entries (position-Y limits,
    /// world-space yaw, yaw-mode hotkey) via the base class's <see cref="OnInitialize"/>
    /// and <see cref="OnRefreshCache"/> extension points. Exposes both the base and custom
    /// entries as static accessors for the static-style access pattern used throughout the mod.
    /// </summary>
    public sealed class HeadTrackingConfig : HeadTrackingConfigBase
    {
        private static HeadTrackingConfig _instance;

        private ConfigEntry<float> _positionLimitY;
        private ConfigEntry<float> _positionLimitYDown;
        private ConfigEntry<bool> _worldSpaceYaw;
        private ConfigEntry<KeyCode> _yawModeKey;

        // Hot-path caches. Refreshed in OnRefreshCache() which the base class triggers
        // on every SettingChanged event. Avoids ConfigEntry<T>.Value dictionary lookups per frame.
        private float _cachedPositionLimitY;
        private float _cachedPositionLimitYDown;
        private bool _cachedWorldSpaceYaw;

        private static HeadTrackingConfig Instance =>
            _instance ?? throw new InvalidOperationException(
                "HeadTrackingConfig.Initialize() has not been called. " +
                "Config must be initialized before accessing properties.");

        // Base-typed view of the singleton. Needed to access inherited instance properties
        // that are shadowed by the `static new` accessors below (otherwise `Instance.UdpPort`
        // would bind to the static shadow property, not the inherited one).
        private static HeadTrackingConfigBase Base => Instance;

        /// <summary>
        /// Initializes the configuration singleton.
        /// Must be called from plugin Awake() before other components are initialized.
        /// </summary>
        public static new void Initialize(ConfigFile config)
        {
            _instance = new HeadTrackingConfig();
            ((HeadTrackingConfigBase)_instance).Initialize(config);
            _instance.OnConfigChanged += OpenTrackReceiver.UpdateProcessorSettings;

            ValheimHeadTrackingPlugin.Log.LogInfo(
                $"Configuration initialized: Port={UdpPort.Value}, EnableOnStartup={EnableOnStartup.Value}");
        }

        /// <summary>
        /// Binds Valheim-specific entries. Invoked by <see cref="HeadTrackingConfigBase.Initialize"/>
        /// after base entries are bound. Base RefreshCache() has already run once by this point;
        /// we call it again here so the first population of our caches sees the bound entries.
        /// </summary>
        protected override void OnInitialize(ConfigFile config)
        {
            _positionLimitY = config.Bind(
                "Position",
                "PositionLimitY",
                0.60f,
                new ConfigDescription(
                    "Maximum upward vertical displacement in meters",
                    new AcceptableValueRange<float>(0f, 1.5f)));

            _positionLimitYDown = config.Bind(
                "Position",
                "PositionLimitYDown",
                0.40f,
                new ConfigDescription(
                    "Maximum downward vertical displacement in meters",
                    new AcceptableValueRange<float>(0f, 1.5f)));

            _worldSpaceYaw = config.Bind(
                "General",
                "WorldSpaceYaw",
                true,
                "Yaw mode: true = horizon-locked yaw (default), false = camera-local. " +
                "Horizon-locked keeps yaw around the world up-axis at any pitch. Camera-local " +
                "rotates around the view's current up-axis, which produces leaning at extreme pitches.");

            _yawModeKey = config.Bind(
                "Hotkeys",
                "YawModeKey",
                KeyCode.PageDown,
                "Key to toggle yaw mode (world-locked vs camera-local)");

            // Base RefreshCache() ran before OnInitialize, so our entries weren't in it.
            // Run it now to populate subclass caches.
            RefreshCache();

            _positionLimitY.SettingChanged += (_, __) => RefreshCache();
            _positionLimitYDown.SettingChanged += (_, __) => RefreshCache();
            _worldSpaceYaw.SettingChanged += (_, __) => RefreshCache();
        }

        protected override void OnRefreshCache()
        {
            // Base RefreshCache() fires once during base Initialize() before OnInitialize
            // binds these entries. Skip until bindings exist.
            if (_positionLimitY == null) return;

            _cachedPositionLimitY = _positionLimitY.Value;
            _cachedPositionLimitYDown = _positionLimitYDown.Value;
            _cachedWorldSpaceYaw = _worldSpaceYaw.Value;
        }

        protected override int DefaultUdpPort =>
            CameraUnlock.Core.Protocol.OpenTrackReceiver.DefaultPort;

        // --- Base entry accessors (static shim over the instance's inherited properties) ---
        public static new ConfigEntry<int> UdpPort => Base.UdpPort;
        public static new ConfigEntry<bool> EnableOnStartup => Base.EnableOnStartup;
        public static new ConfigEntry<float> YawSensitivity => Base.YawSensitivity;
        public static new ConfigEntry<float> PitchSensitivity => Base.PitchSensitivity;
        public static new ConfigEntry<float> RollSensitivity => Base.RollSensitivity;
        public static new ConfigEntry<bool> InvertYaw => Base.InvertYaw;
        public static new ConfigEntry<bool> InvertPitch => Base.InvertPitch;
        public static new ConfigEntry<bool> InvertRoll => Base.InvertRoll;
        public static new ConfigEntry<KeyCode> RecenterKey => Base.RecenterKey;
        public static new ConfigEntry<KeyCode> ToggleKey => Base.ToggleKey;
        public static new ConfigEntry<KeyCode> PositionToggleKey => Base.PositionToggleKey;
        public static new ConfigEntry<KeyCode> ReticleToggleKey => Base.ReticleToggleKey;
        public static new ConfigEntry<bool> EnableAimDecoupling => Base.EnableAimDecoupling;
        public static new ConfigEntry<bool> ShowDecoupledCrosshair => Base.ShowDecoupledCrosshair;

        // --- Base cached values (static shim) ---
        public static new float CachedYawSensitivity => Base.CachedYawSensitivity;
        public static new float CachedPitchSensitivity => Base.CachedPitchSensitivity;
        public static new float CachedRollSensitivity => Base.CachedRollSensitivity;
        public static new bool CachedInvertYaw => Base.CachedInvertYaw;
        public static new bool CachedInvertPitch => Base.CachedInvertPitch;
        public static new bool CachedInvertRoll => Base.CachedInvertRoll;
        public static new bool CachedEnableAimDecoupling => Base.CachedEnableAimDecoupling;
        public static new bool CachedShowDecoupledCrosshair => Base.CachedShowDecoupledCrosshair;

        // --- Valheim-specific entries ---
        public static ConfigEntry<float> PositionLimitY => Instance._positionLimitY;
        public static ConfigEntry<float> PositionLimitYDown => Instance._positionLimitYDown;
        public static ConfigEntry<bool> WorldSpaceYaw => Instance._worldSpaceYaw;
        public static ConfigEntry<KeyCode> YawModeKey => Instance._yawModeKey;

        // --- Valheim-specific cached values ---
        public static float CachedPositionLimitY => Instance._cachedPositionLimitY;
        public static float CachedPositionLimitYDown => Instance._cachedPositionLimitYDown;
        public static bool CachedWorldSpaceYaw => Instance._cachedWorldSpaceYaw;
    }
}
