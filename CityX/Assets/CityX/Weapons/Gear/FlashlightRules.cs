using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The flashlight's beam (docs/FLASHLIGHT.md), from how games light theirs: a spot light whose shape is a cookie -- a bright hot spot
    /// melting into a soft wide spill -- warm white, reaching far, casting soft shadows.
    /// </summary>
    public static class FlashlightRules
    {
        /// <summary>
        /// How far the beam reaches, metres (the city is 1.3 times life size), and its brightness. The beam is a carried light
        /// (<see cref="CityX.View.CityLightLayers.Beam"/>): under the ground it does not fall off with the distance, so what it lights on
        /// the wall beside the hand it lights as brightly far down a tunnel, until its reach ends it.
        /// </summary>
        public const float Range = 45.0f;

        public const float Intensity = 90.0f;

        /// <summary>
        /// The glow round a torch lit under the ground: the light its beam throws back off the walls round it. The beam only lights what
        /// is in front of it, some metres on; in a tunnel it is the glow that shows the walls beside whoever carries it, and the carrier.
        /// A dim light all round the lens that casts no shadows: how far it reaches, metres, and its brightness as a share of the beam's.
        /// </summary>
        public const float GlowReach = 8.0f;

        public const float GlowShare = 0.09f;

        /// <summary>
        /// The glow's brightness where the torch is <paramref name="darkness"/> out of the sky's light (0 under the sky, 1 in a tunnel).
        /// Under the sky there are no walls round the torch to throw its beam back: there the glow was a pool of light at the carrier's feet.
        /// </summary>
        public static float GlowIntensity(float darkness)
        {
            return Intensity * GlowShare * Mathf.Clamp01(darkness);
        }

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

        /// <summary>
        /// The shaft is flat wedges: from the side they are a cone of light in the air, looked along they are slabs. It is whole to an eye
        /// this far off the beam's line or more (the cosine of the angle between the beam and the line from the lens to the eye), and gone
        /// to one this near it.
        /// </summary>
        public const float ShaftSideOn = 0.5f;

        public const float ShaftEndOn = 0.9f;

        /// <summary>
        /// How much of the shaft an eye is shown, by <paramref name="towardEye"/>: the cosine between the beam and the line from the lens to
        /// the eye (1 looking into the beam, 0 from the side, -1 from behind the hand, where the game's camera stands).
        /// </summary>
        public static float ShaftShown(float towardEye)
        {
            return 1.0f - Mathf.SmoothStep(0.0f, 1.0f, (Mathf.Abs(towardEye) - ShaftSideOn) / (ShaftEndOn - ShaftSideOn));
        }

        /// <summary>The glow on the lens, metres across, and how much brighter it flares when the beam points at the eye.</summary>
        public const float GlareSize = 0.32f;

        public const float GlareFlare = 4.0f;

        /// <summary>How far the hot spot reaches out across the cookie (0 the beam's middle, 1 its edge).</summary>
        public static float HotSpotShare => Mathf.Tan(HotSpotDegrees * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(SpillDegrees * 0.5f * Mathf.Deg2Rad);

        /// <summary>
        /// How bright the spill is beside the hot spot (the hot spot 1), in light, not in the cookie's texels (<see cref="CookieTexelAt"/>).
        /// It is the spill that lights the walls and the floor of a tunnel walked along; the hot spot lands far down it.
        /// </summary>
        public const float SpillShare = 0.55f;

        /// <summary>The hot spot is full across this share of its own width, and has melted into the spill by this many of them.</summary>
        public const float HotSpotFullShare = 0.4f;

        public const float HotSpotMeltShare = 1.8f;

        /// <summary>
        /// The cookie's brightness <paramref name="r"/> of the way out from the beam's middle (1 its edge): the hot spot, full in its middle,
        /// melting into the spill, and the spill falling smoothly away to nothing at the edge. One area of light that only ever fades going
        /// out: a reflector's ring and ripples, and a hot spot with an edge, all drew circles on whatever the beam fell on.
        /// </summary>
        public static float CookieAt(float r)
        {
            if (r >= 1.0f)
            {
                return 0.0f;
            }
            float hot = HotSpotShare;
            float spot = 1.0f - Mathf.SmoothStep(0.0f, 1.0f, (r - hot * HotSpotFullShare) / (hot * (HotSpotMeltShare - HotSpotFullShare)));
            float spill = SpillShare * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, r));
            return spill + (1.0f - spill) * spot;
        }

        /// <summary>
        /// What the cookie's texture holds for <see cref="CookieAt"/>. The texture is an sRGB one (GlowShapes paints every glow so) and a light
        /// decodes what it reads from it, so the value goes in encoded and the light gets <see cref="CookieAt"/> itself: put in as it is, a
        /// spill of a half lights as a fifth.
        /// </summary>
        public static float CookieTexelAt(float r)
        {
            return Mathf.LinearToGammaSpace(CookieAt(r));
        }
    }
}
