using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using ValheimHeadTracking.Config;
using ValheimHeadTracking.Legacy;

namespace ValheimHeadTracking
{
    /// <summary>
    /// The settings live in BepInEx\config\CameraUnlock.ini, read and written by core's config
    /// owner, with rows set to default following the player's Defaults.ini. Nothing is bound
    /// through BepInEx's ConfigFile at runtime, so ConfigurationManager does not list them. While
    /// CameraUnlock.ini is absent the owner imports the plugin's .cfg, the file every earlier build
    /// read, through the frozen v0.3.0 reader, and never writes that file.
    /// </summary>
    public static class HeadTrackingConfig
    {
        private static ConfigOwner<ValheimConfig> _owner;
        private static ValheimConfig _current;

        // Messages for the player, held until MessageHud exists: the config loads at the main
        // menu, where the game has no HUD to show them on.
        private static readonly Queue<string> PendingMessages = new Queue<string>();

        public static ValheimConfig Current =>
            _current ?? throw new InvalidOperationException(
                "HeadTrackingConfig.Load() has not been called. " +
                "Config must be loaded before accessing properties.");

        public static string ConfigPath => Path.Combine(Paths.ConfigPath, "CameraUnlock.ini");

        /// <summary>
        /// Must be called from plugin Awake() before other components are initialized.
        /// </summary>
        /// <param name="pluginConfig">The plugin's Config, whose file is the legacy file.</param>
        public static void Load(ConfigFile pluginConfig)
        {
            _owner = new ConfigOwner<ValheimConfig>(new ConfigOwnerOptions<ValheimConfig>
            {
                Path = ConfigPath,
                Table = ValheimConfig.Table(),
                Import = LegacyConfigImport.For(pluginConfig),
                LegacySourcePath = pluginConfig.ConfigFilePath,
                Header = new RenderHeader(ValheimConfig.DisplayName),
                Defaults = DefaultsFile.PerUser(),
                StatusSink = PendingMessages.Enqueue,
            });

            ConfigLoadResult<ValheimConfig> loaded = _owner.Load();
            _current = loaded.Config;

            // The owner writes each diagnostic as "<path>: <description>" among lines that only
            // report what it did, so the complaints are picked out by their text.
            var complaints = new HashSet<string>();
            foreach (CanonicalDiagnostic diagnostic in loaded.Diagnostics)
                complaints.Add(ConfigPath + ": " + diagnostic.Describe());
            bool usable = loaded.Status == ConfigLoadStatus.Canonical
                          || loaded.Status == ConfigLoadStatus.Migrated
                          || loaded.Status == ConfigLoadStatus.Created;
            foreach (string line in loaded.Log)
            {
                if (usable && !complaints.Contains(line)) ValheimHeadTrackingPlugin.Log.LogInfo(line);
                else ValheimHeadTrackingPlugin.Log.LogWarning(line);
            }
            ValheimHeadTrackingPlugin.Log.LogInfo("Config " + ConfigPath + ": " + loaded.Status);
        }

        /// <summary>
        /// Called after the new value is already applied. A save that fails is logged, the owner
        /// tells the player why, and the session keeps the new value.
        /// </summary>
        public static void Save(Action<ValheimConfig> change)
        {
            ConfigSaveResult saved = _owner.Save(change);
            if (saved.Status == ConfigSaveStatus.Saved)
            {
                // A row that held default and now holds a value, so it stops following
                // Defaults.ini in this game.
                foreach (string line in saved.Log) ValheimHeadTrackingPlugin.Log.LogInfo(line);
                return;
            }
            foreach (string line in saved.Log) ValheimHeadTrackingPlugin.Log.LogWarning(line);
            ValheimHeadTrackingPlugin.Log.LogWarning(ConfigPath + ": " + saved.Status + ": " + saved.Reason
                                                     + " The change applies to this session only.");
        }

        /// <summary>Every message for the player not yet shown, one per line, or null for none.</summary>
        public static string TakePendingMessages()
        {
            if (PendingMessages.Count == 0) return null;
            string text = string.Join("\n", PendingMessages.ToArray());
            PendingMessages.Clear();
            return text;
        }
    }
}
