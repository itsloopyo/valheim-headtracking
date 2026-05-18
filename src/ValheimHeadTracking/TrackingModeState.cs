namespace ValheimHeadTracking
{
    public enum TrackingMode
    {
        Full = 0,
        RotationOnly = 1,
        PositionOnly = 2
    }

    public static class TrackingModeState
    {
        public static TrackingMode Mode { get; private set; } = TrackingMode.Full;

        public static bool IsRotationEnabled => Mode != TrackingMode.PositionOnly;
        public static bool IsPositionEnabled => Mode != TrackingMode.RotationOnly;

        public static TrackingMode Cycle()
        {
            Mode = (TrackingMode)(((int)Mode + 1) % 3);
            return Mode;
        }

        public static string Describe(TrackingMode mode)
        {
            switch (mode)
            {
                case TrackingMode.Full: return "6DOF (rotation + position)";
                case TrackingMode.RotationOnly: return "3DOF rotation only";
                case TrackingMode.PositionOnly: return "3DOF position only";
                default: return mode.ToString();
            }
        }
    }
}
