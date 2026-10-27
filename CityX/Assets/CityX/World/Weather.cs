using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// The RULES of rain, as pure functions: how wet things get and how fast,
    /// what wetness does to grip, flames and people, and how far away a
    /// lightning strike is heard and seen. No scene, no state, no engine
    /// objects -- so every number a player will feel can be checked without
    /// loading anything, and the visuals (RainEffect) and the gameplay
    /// (locomotion, climbing, the flamethrower, pedestrians) read the SAME
    /// numbers instead of each inventing their own idea of "wet".
    ///
    /// Distilled from how shipped games treat rain (see docs/RAIN.md for the
    /// sources): Lagarde's Remember Me rain (wetness rises fast and falls
    /// slowly; wet surfaces darken and gain sheen, sheen dropping out before
    /// the darkening does), Breath of the Wild (rain makes surfaces slippery,
    /// people run for shelter), GTA (wet roads cost traction) and the
    /// speed-of-sound rule every thunderstorm follows.
    /// </summary>
    public static class Weather
    {
        // --- intensity ---

        public const float LightRain = 0.25f;
        public const float ModerateRain = 0.55f;
        public const float HeavyRain = 1.0f;

        /// <summary>Below this the rain is a drizzle: no lightning, however long it lasts.</summary>
        public const float ThunderstormThreshold = 0.6f;

        /// <summary>
        /// Reads a console word or a number into an intensity: "light",
        /// "moderate", "heavy", or 0..1. False for anything else.
        /// </summary>
        public static bool TryParseIntensity(string text, out float intensity)
        {
            intensity = HeavyRain;
            if (string.IsNullOrEmpty(text))
            {
                return true;   // plain "rain" means a proper downpour, as it always has
            }
            switch (text.Trim().ToLowerInvariant())
            {
                case "light": case "drizzle": intensity = LightRain; return true;
                case "moderate": case "medium": intensity = ModerateRain; return true;
                case "heavy": case "storm": case "downpour": intensity = HeavyRain; return true;
            }
            if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value) &&
                value > 0.0f && value <= 1.0f)
            {
                intensity = value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Drops crossing each square metre of open air per second. Heavy rain
        /// is the density the original effect had (900 drops alive in a 40m
        /// box); lighter rain thins it out. Zero at zero.
        /// </summary>
        public static float DropFlux(float intensity)
        {
            return intensity <= 0.0f ? 0.0f : 0.06f + 0.69f * Mathf.Clamp01(intensity);
        }

        /// <summary>How much of the storm's mood (dim sun, tight fog, grey grade) an intensity earns -- light rain is grey, not dark.</summary>
        public static float StormTarget(float intensity)
        {
            if (intensity <= 0.0f)
            {
                return 0.0f;
            }
            // Exactly 1 at full intensity: the storm ramp snaps to its target, and
            // a test (and the fog restore) waits for that exact value.
            return intensity >= 1.0f ? 1.0f : 0.35f + 0.65f * intensity;
        }

        /// <summary>Rain sound level: a drizzle is a hush, a downpour a roar.</summary>
        public static float RainVolume(float intensity)
        {
            return intensity <= 0.0f ? 0.0f : Mathf.Lerp(0.3f, 1.0f, Mathf.Clamp01(intensity));
        }

        // --- wetness: rises fast, falls slowly ---

        /// <summary>Seconds of steady heavy rain to take dry ground to fully soaked.</summary>
        public const float SoakSeconds = 8.0f;

        /// <summary>
        /// Seconds for fully soaked ground to dry out once it stops raining.
        /// Long on purpose: in Remember Me's rain "wet level increases rapidly
        /// when rain starts, decreases slowly when it stops" -- the city
        /// glistens for a good while after the sky clears.
        /// </summary>
        public const float DrySeconds = 120.0f;

        /// <summary>The wettest a given rain can make things: a drizzle only ever dampens.</summary>
        public static float WetnessCeiling(float intensity)
        {
            return intensity <= 0.0f ? 0.0f : Mathf.Lerp(0.45f, 1.0f, Mathf.Clamp01(intensity));
        }

        /// <summary>One step of the wetness state.</summary>
        public static float StepWetness(float wetness, float intensity, float deltaTime)
        {
            float ceiling = WetnessCeiling(intensity);
            if (wetness < ceiling)
            {
                float rate = (0.5f + 0.5f * Mathf.Clamp01(intensity)) / SoakSeconds;
                return Mathf.Min(ceiling, wetness + rate * deltaTime);
            }
            return Mathf.Max(ceiling, wetness - deltaTime / DrySeconds);
        }

        /// <summary>
        /// How much of a surface's SHEEN is left at a wetness. Squared, so the
        /// shine goes out of a drying street well before the darkening does --
        /// "specular vanishes faster than diffuse" (Lagarde, wet surfaces).
        /// </summary>
        public static float SheenFactor(float wetness)
        {
            float w = Mathf.Clamp01(wetness);
            return w * w;
        }

        /// <summary>
        /// Ripples on a wet surface: none on damp ground, more as it floods
        /// and as the rain thickens. Only where enough water has gathered do
        /// rings form (Lagarde: "rain causes ripples only where there is
        /// sufficient accumulated water").
        /// </summary>
        public static float RippleChance(float wetness, float intensity)
        {
            float pooled = Mathf.Clamp01((wetness - 0.35f) / 0.5f);
            return pooled * pooled * (0.15f + 0.45f * Mathf.Clamp01(intensity));
        }

        // --- the player's own body ---

        /// <summary>Seconds of open, heavy rain to soak a body through.</summary>
        public const float BodySoakSeconds = 6.0f;

        /// <summary>Seconds for a soaked body to fade back to dry (owner, 2026-10-27: "over the course of 1 minute").</summary>
        public const float BodyDrySeconds = 60.0f;

        /// <summary>How much faster it dries standing in the sun ("if you are in sunlight then it speeds up to 1.5x faster").</summary>
        public const float SunDryingSpeedUp = 1.5f;

        /// <summary>
        /// One step of how wet a person is. Rain ON them soaks them (in proportion to how hard it is falling on that spot), and the minute of
        /// drying starts again from however wet that leaves them; otherwise they fade back to dry, faster in the sun.
        /// </summary>
        public static float StepBodyWetness(float wetness, float rainOnBody, bool inSunlight, float deltaTime)
        {
            if (rainOnBody > 0.0f)
            {
                return Mathf.Min(1.0f, wetness + rainOnBody * deltaTime / BodySoakSeconds);
            }
            float speed = inSunlight ? SunDryingSpeedUp : 1.0f;
            return Mathf.Max(0.0f, wetness - speed * deltaTime / BodyDrySeconds);
        }

        // --- what wetness does to the player ---

        /// <summary>The grip a soaked, open surface keeps -- dry ground is 1.</summary>
        public const float WetTractionFloor = 0.5f;

        /// <summary>
        /// Multiplier on how hard the feet can push or brake on a surface of
        /// this wetness. Wet ground costs traction (GTA's wet roads, every
        /// skid): starting is slower and stopping slides.
        /// </summary>
        public static float GroundTraction(float surfaceWetness)
        {
            return Mathf.Lerp(1.0f, WetTractionFloor, Mathf.Clamp01(surfaceWetness));
        }

        /// <summary>Multiplier on stamina drained while gripping a wet wall or ladder -- the same climb costs more (Breath of the Wild).</summary>
        public static float WallStaminaDrain(float surfaceWetness)
        {
            return 1.0f + Mathf.Clamp01(surfaceWetness);
        }

        /// <summary>Metres per second a wet wall lets a gripping body slide DOWN -- rain on the surface, hands and feet slipping.</summary>
        public const float WetWallSlipSpeed = 1.1f;

        public static float WallSlipSpeed(float surfaceWetness)
        {
            return WetWallSlipSpeed * Mathf.Clamp01(surfaceWetness);
        }

        /// <summary>How much of its bite a flame keeps with rain falling on it -- damage and reach both scale by this.</summary>
        public static float FlameStrength(float rainOnFlame)
        {
            return 1.0f - 0.5f * Mathf.Clamp01(rainOnFlame);
        }

        // --- what rain does to people ---

        /// <summary>How much faster a pedestrian walks with rain on them: 1 dry, up to 1.6 in a downpour.</summary>
        public static float PedestrianHurry(float rainOnPerson)
        {
            return Mathf.Lerp(1.0f, 1.6f, Mathf.Clamp01(rainOnPerson));
        }

        /// <summary>
        /// How much rain has to be falling on a pedestrian before they bother
        /// looking for cover -- a drizzle is ignored.
        /// </summary>
        public const float ShelterSeekThreshold = 0.3f;

        // --- the storm overhead ---

        /// <summary>Metres per second, dry air at 20C.</summary>
        public const float SpeedOfSound = 343.0f;

        /// <summary>Seconds between a flash and its thunder: light is instant, sound is not.</summary>
        public static float ThunderDelaySeconds(float distanceMetres)
        {
            return Mathf.Max(0.0f, distanceMetres) / SpeedOfSound;
        }

        /// <summary>How hard a strike lights the sky: full within 150m, a faint flicker by 2km.</summary>
        public static float FlashStrength(float distanceMetres)
        {
            return Mathf.Lerp(1.0f, 0.12f, Mathf.Clamp01((distanceMetres - 150.0f) / 1850.0f));
        }

        /// <summary>Thunder level by distance: a house-rattling crack up close, a murmur far off.</summary>
        public static float ThunderVolume(float distanceMetres)
        {
            return Mathf.Clamp(1.0f - distanceMetres / 2400.0f, 0.15f, 1.0f);
        }

        /// <summary>
        /// Where thunder's sound is cut off, in hertz. Close thunder is bright
        /// and punchy; distance strips the high end until only a low roll is
        /// left.
        /// </summary>
        public static float ThunderBrightnessHz(float distanceMetres)
        {
            return Mathf.Lerp(9000.0f, 260.0f, Mathf.Clamp01((distanceMetres - 100.0f) / 1600.0f));
        }

        /// <summary>
        /// Where the rain and thunder are cut off when the listener is under
        /// cover, in hertz: indoors "you mostly hear filtered low end through
        /// structure". 22kHz is unfiltered.
        /// </summary>
        public static float ShelteredCutoffHz(float shelter)
        {
            return Mathf.Lerp(22000.0f, 1300.0f, Mathf.Clamp01(shelter));
        }

        /// <summary>How loud things heard from under cover are, against the open: rain on a roof is quieter than rain on you.</summary>
        public static float ShelteredVolume(float shelter)
        {
            return Mathf.Lerp(1.0f, 0.6f, Mathf.Clamp01(shelter));
        }
    }
}
