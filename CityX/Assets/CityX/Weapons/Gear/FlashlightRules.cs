using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The flashlight's beam (docs/FLASHLIGHT.md), from how games light theirs: a spot light whose shape is a cookie -- a bright hot spot in a
    /// soft wide spill, the faint ring a reflector's rim throws and its faint ripples -- warm white, reaching far, casting soft shadows.
    /// </summary>
    public static class FlashlightRules
    {
        /// <summary>
        /// How far the beam reaches, metres (the city is 1.3 times life size), and its brightness: the game's lights fall off with the square of
        /// the distance, so 90 is 0.9 at 10 m, a little brighter than a lamp's light on the street.
        /// </summary>
        public const float Range = 45.0f;

        public const float Intensity = 90.0f;

        /// <summary>The beam's full angle, degrees: its soft spill, and the hot spot in its middle (a reflector torch's tight bright centre).</summary>
        public const float SpillDegrees = 55.0f;

        public const float HotSpotDegrees = 16.0f;

        /// <summary>Warm white: a filament bulb's, not an LED's blue (the torches of Alan Wake and Resident Evil are warm).</summary>
        public static readonly Color Colour = new Color(1.0f, 0.92f, 0.78f);

        /// <summary>The shadows start this far out from the lens, metres: the hand holding it never shades it.</summary>
        public const float ShadowNearPlane = 0.25f;

        /// <summary>
        /// The shaft of light in the air: how long, as a share of the reach, how wide across, degrees (the hot spot's cone and a little more: the
        /// bright core of the beam is what shows in the air), and how bright each of its wedges is at the lens (it fades along its length).
        /// </summary>
        public const float ShaftShare = 0.22f;

        public const float ShaftDegrees = 24.0f;

        public const float ShaftStrength = 0.07f;

        /// <summary>The glow on the lens, metres across, and how much brighter it flares when the beam points at the eye.</summary>
        public const float GlareSize = 0.32f;

        public const float GlareFlare = 4.0f;

        /// <summary>How far the hot spot reaches out across the cookie (0 the beam's middle, 1 its edge).</summary>
        public static float HotSpotShare => Mathf.Tan(HotSpotDegrees * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(SpillDegrees * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        /// The cookie's brightness <paramref name="r"/> of the way out from the beam's middle (1 its edge): the hot spot, full and soft-edged; a faint
        /// ring just past it where the reflector's rim throws its light; the spill falling smoothly away to nothing at the edge; and the reflector's
        /// faint ripples over it all (what makes a game's torch read as a torch and not a stage light).
        /// </summary>
        public static float CookieAt(float r)
        {
            if (r >= 1.0f)
            {
                return 0.0f;
            }
            float hot = HotSpotShare;
            float spot = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, (r - hot * 0.7f) / (hot * 0.6f));
            float ring = (r - hot * 1.25f) / 0.035f;
            float spill = 0.4f * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, r));
            float ripples = 1.0f + 0.04f * Mathf.Sin(r * 48.0f);
            return Mathf.Clamp01((Mathf.Max(spot, spill) + 0.14f * Mathf.Exp(-ring * ring)) * ripples);
        }
    }
}
