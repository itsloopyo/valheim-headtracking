using BepInEx.Logging;

namespace ValheimHeadTracking
{
    /// <summary>
    /// The two plugin members v0.3.0's HeadTrackingConfig.cs names beside the settings, so the
    /// published file compiles into the test unchanged. Neither changes what the file reads.
    /// </summary>
    internal static class ValheimHeadTrackingPlugin
    {
        internal static ManualLogSource Log = new ManualLogSource("oracle");
    }

    /// <summary>
    /// v0.3.0 pushed the settings into its processors here whenever one changed. The oracle reads
    /// the settings straight off the entries, and <c>LegacyStartup</c> turns them into what that
    /// push set up.
    /// </summary>
    internal static class OpenTrackReceiver
    {
        public static void UpdateProcessorSettings()
        {
        }
    }
}
