namespace CityX.Character
{
    /// <summary>How much of another player's body is worked out and drawn (CrowdDetail).</summary>
    public enum CrowdTier
    {
        /// <summary>Everything: near the camera, one of the nearest.</summary>
        Full,

        /// <summary>Further off or past the nearest few: no shadow, the costliest posing details left out.</summary>
        Light,

        /// <summary>Not on the screen: nothing drawn, nothing posed that only shows.</summary>
        Unseen,
    }

    /// <summary>
    /// The crowd's level of detail (owner, 2026-11-25: "something that will not tank performance in large crowds of people remember there is a
    /// hub"; docs/CROWD_TEST.md): which tier another player's body is in, from how far it is from the camera, how many are nearer, and whether
    /// it is on the screen. Pure.
    /// </summary>
    public static class CrowdTiers
    {
        /// <summary>Within this of the camera, metres, a body is shown in full ...</summary>
        public const float FullWithin = 15.0f;

        /// <summary>... if no more than this many are nearer (a packed hub shows the nearest in full, the rest lighter).</summary>
        public const int MostInFull = 10;

        /// <summary>How often the crowd is ranked by distance, seconds.</summary>
        public const float RankEvery = 0.25f;

        /// <param name="metres">From the camera.</param>
        /// <param name="nearer">How many of the crowd are nearer the camera.</param>
        /// <param name="seen">Whether it was on the screen last frame.</param>
        public static CrowdTier For(float metres, int nearer, bool seen)
        {
            if (!seen)
            {
                return CrowdTier.Unseen;
            }
            return metres <= FullWithin && nearer < MostInFull ? CrowdTier.Full : CrowdTier.Light;
        }
    }

    /// <summary>A part of a body that does less for a body further down the crowd's tiers (CrowdDetail tells it).</summary>
    public interface ICrowdDetailed
    {
        void ShowAt(CrowdTier tier);
    }
}
