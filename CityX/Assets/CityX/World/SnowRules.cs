using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// The rules of snow (docs/SNOW.md; owner, 2026-11-05: "add snowy weather mechanics, research how video games do snow weather and make it a weather
    /// option"), pure, as <see cref="Weather"/> is for rain. What the games share is a movement cost, tracks that last, and less to see and hear. The
    /// numbers are all first guesses.
    /// </summary>
    public static class SnowRules
    {
        public const float LightSnow = 0.25f;
        public const float ModerateSnow = 0.55f;
        public const float Blizzard = 1.0f;

        /// <summary>Reads a console word or a number into an intensity: "light", "moderate", "heavy" or "blizzard", or 0..1. False for anything else. Plain "snow" is moderate.</summary>
        public static bool TryParseIntensity(string text, out float intensity)
        {
            intensity = ModerateSnow;
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            switch (text.Trim().ToLowerInvariant())
            {
                case "light": case "flurry": intensity = LightSnow; return true;
                case "moderate": case "medium": intensity = ModerateSnow; return true;
                case "heavy": case "blizzard": case "storm": intensity = Blizzard; return true;
            }
            if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0.0f && value <= 1.0f)
            {
                intensity = value;
                return true;
            }
            return false;
        }

        // --- what lies on the ground ---

        /// <summary>How long a blizzard takes to bury a clear city, seconds; lighter snow takes longer in proportion.</summary>
        public const float BlizzardBurySeconds = 120.0f;

        /// <summary>How long a whole cover takes to melt with nothing falling, seconds.</summary>
        public const float MeltSeconds = 300.0f;

        /// <summary>How much quicker it goes in the sun, and in the rain.</summary>
        public const float SunMeltSpeedUp = 2.0f;

        public const float RainMeltSpeedUp = 4.0f;

        /// <summary>How much of the city a snowfall can cover: a flurry only dusts it, however long it lasts.</summary>
        public static float CoverCeiling(float intensity) => intensity <= 0.0f ? 0.0f : Mathf.Lerp(0.35f, 1.0f, Mathf.Clamp01(intensity));

        /// <summary>The cover after <paramref name="deltaTime"/>: up toward what is falling can reach, down when more lies than that. <paramref name="sunshine"/> and <paramref name="rain"/> are 0..1.</summary>
        public static float StepCover(float cover, float intensity, float sunshine, float rain, float deltaTime)
        {
            float ceiling = CoverCeiling(intensity);
            if (cover < ceiling && rain <= 0.0f)
            {
                return Mathf.Min(ceiling, cover + Mathf.Clamp01(intensity) / BlizzardBurySeconds * deltaTime);
            }
            float speed = Mathf.Lerp(1.0f, SunMeltSpeedUp, Mathf.Clamp01(sunshine)) * Mathf.Lerp(1.0f, RainMeltSpeedUp, Mathf.Clamp01(rain));
            return Mathf.Max(rain > 0.0f ? 0.0f : ceiling, cover - speed / MeltSeconds * deltaTime);
        }

        // --- walking on it ---

        /// <summary>The share of normal grip on a full cover: slippery, as wet ground is, but less than soaked ground (<see cref="Weather.WetTractionFloor"/>).</summary>
        public const float TractionFloor = 0.65f;

        /// <summary>The share of normal speed through a full cover: deep snow drags at the legs.</summary>
        public const float PaceFloor = 0.85f;

        /// <summary>Grip on ground with this much snow on it.</summary>
        public static float Traction(float cover) => Mathf.Lerp(1.0f, TractionFloor, Mathf.Clamp01(cover));

        /// <summary>Pace through ground with this much snow on it: a dusting costs nothing.</summary>
        public static float Pace(float cover) => Mathf.Lerp(1.0f, PaceFloor, Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(0.3f, 1.0f, cover)));

        /// <summary>Tracks show from this much cover: on a bare dusting there is nothing to press.</summary>
        public const float TracksFromCover = 0.2f;

        /// <summary>How long a print lasts before it fills back in, seconds.</summary>
        public const float PrintSeconds = 45.0f;

        // --- the air ---

        /// <summary>How much of a flame a snowfall takes away, as a share of what rain of that strength would: it melts before it reaches the fire.</summary>
        public const float QuenchShare = 0.5f;

        /// <summary>The grey lid of cloud: light snow is already overcast, a blizzard nearly white-out.</summary>
        public static float Overcast(float intensity) => intensity <= 0.0f ? 0.0f : Mathf.Lerp(0.4f, 0.9f, Mathf.Clamp01(intensity));

        /// <summary>The fog at a blizzard's height, metres: past the end of it nothing shows.</summary>
        public const float BlizzardFogStart = 35.0f;

        public const float BlizzardFogEnd = 120.0f;

        public static float FogStart(float clear, float intensity) => Mathf.Lerp(clear, BlizzardFogStart, FogShare(intensity));

        public static float FogEnd(float clear, float intensity) => Mathf.Lerp(clear, BlizzardFogEnd, FogShare(intensity));

        private static float FogShare(float intensity) => Mathf.Clamp01(intensity) * Mathf.Clamp01(intensity);

        /// <summary>The sun through the cloud, as a share of clear.</summary>
        public static float SunThrough(float mood) => Mathf.Lerp(1.0f, 0.55f, Mathf.Clamp01(mood));

        /// <summary>The wind that carries it, m/s.</summary>
        public static float WindSpeed(float intensity) => Mathf.Lerp(0.4f, 5.0f, Mathf.Clamp01(intensity) * Mathf.Clamp01(intensity));

        /// <summary>How loud the wind is out in the open.</summary>
        public static float WindVolume(float intensity) => intensity <= 0.0f ? 0.0f : Mathf.Lerp(0.12f, 0.7f, Mathf.Clamp01(intensity));
    }
}
