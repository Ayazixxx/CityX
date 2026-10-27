using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Where the sun is and whether it is shining, as a plain static holder (the <see cref="CurrentWeather"/> bridge shape): DayNightCycle
    /// (CityX.View) publishes it every time it applies a sky and clears it when it goes away, and CityX.Character reads it without a
    /// reference to the view. A scene without a sky cycle -- every test that is not about the sun -- reads no sunshine at all.
    /// </summary>
    public static class CurrentSky
    {
        /// <summary>The way to the sun, unit length.</summary>
        public static Vector3 TowardSun = Vector3.up;

        /// <summary>0 with no direct sun (night, under a storm's cloud lid) .. 1 with the sun up in a clear sky.</summary>
        public static float Sunshine;

        /// <summary>How much sunshine counts as being out in the sun, and how high it must stand (the sine of its elevation).</summary>
        public const float SunshineToCount = 0.5f;
        public const float LowestSun = 0.05f;

        /// <summary>Whether the sun shines on anything the sky above it leaves open (what is in the way is the caller's to look for).</summary>
        public static bool SunIsOut => Sunshine >= SunshineToCount && TowardSun.y > LowestSun;

        /// <summary>The sunshine of a sky: the sun above the horizon, less what an overcast hides.</summary>
        public static float SunshineOf(float sunElevationSine, float overcast)
        {
            return sunElevationSine > 0.0f ? 1.0f - Mathf.Clamp01(overcast) : 0.0f;
        }

        /// <summary>Back to no sun -- what a scene without a sky cycle reads.</summary>
        public static void Clear()
        {
            TowardSun = Vector3.up;
            Sunshine = 0.0f;
        }
    }
}
