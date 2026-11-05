using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The ticks of a held spell (a beam's burn, a beam's heal), counted from the frames' own time so they come the same however the frames fall.</summary>
    public static class SpellTicks
    {
        /// <summary>
        /// The ticks due after <paramref name="deltaTime"/> more seconds, one every <paramref name="tickSeconds"/>, <paramref name="sinceTick"/>
        /// carrying the time since the last (start it at <paramref name="tickSeconds"/> for the first to land at once).
        /// </summary>
        public static int Due(ref float sinceTick, float deltaTime, float tickSeconds)
        {
            sinceTick += Mathf.Max(0.0f, deltaTime);
            int due = Mathf.FloorToInt(sinceTick / tickSeconds + 1e-4f);
            sinceTick -= due * tickSeconds;
            return due;
        }

        /// <summary>
        /// The same clock while the spell is off: it runs on, but no further than one tick. So a spell let go and started again lands its
        /// first tick at once only when a tick's time has passed since its last, and tapping the click hits no oftener than holding it.
        /// </summary>
        public static void Rest(ref float sinceTick, float deltaTime, float tickSeconds)
        {
            sinceTick = Mathf.Min(sinceTick + Mathf.Max(0.0f, deltaTime), tickSeconds);
        }
    }
}
