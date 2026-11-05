using System;

namespace CityX.World
{
    /// <summary>
    /// THE CLOCK EVERY TRAFFIC LIGHT RUNS ON (docs/TRAFFIC_CONTROL.md). A light is a pure function of it (SignalTiming), so with one clock for the whole session every
    /// player sees every light the same. In a session it is the session's own time (CityX.Character's NetworkedSignalClock gives it); alone, this machine's.
    /// </summary>
    public static class SignalClock
    {
        /// <summary>The session's shared seconds, while in one; null alone.</summary>
        public static Func<double> Shared;

        /// <summary>This machine's own seconds, kept up by whoever draws the lights.</summary>
        public static double Local;

        public static double Seconds => Shared != null ? Shared() : Local;
    }
}
