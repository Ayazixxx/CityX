using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Everything the sky looks like at a given moment, as a pure function of
    /// the time of day. Ported from city/day_night_cycle.gd.
    ///
    /// A PURE FUNCTION, WITH THE APPLYING DONE ELSEWHERE. The Godot original
    /// computed and applied in one _process, which meant the only way to ask
    /// "what does dusk look like" was to sit and wait for dusk. Every value
    /// here can be sampled at any moment of the cycle instantly, which is what
    /// makes a 34-minute cycle testable at all.
    ///
    /// The sun and moon share one rotation axis and stay exactly 180 degrees
    /// apart, so they trace the same arc and whichever is above the horizon
    /// lights the world while the other fades out -- rather than both
    /// contributing at once, which reads as a physically wrong double noon.
    /// </summary>
    public static class SkyCycle
    {
        public const float DaySeconds = 20.0f * 60.0f;
        public const float NightSeconds = 14.0f * 60.0f;
        public const float CycleSeconds = DaySeconds + NightSeconds;

        /// <summary>
        /// Mid-morning rather than the exact instant of sunrise, so the world
        /// opens somewhere lit instead of at the dimmest moment of the day.
        /// </summary>
        public const float StartSeconds = DaySeconds * 0.25f;

        /// <summary>
        /// Carried from the massing scene's fixed sun, so noon shadows still
        /// fall the direction the concrete was tuned to look right under.
        /// </summary>
        public const float AzimuthDegrees = 145.0f;

        /// <summary>
        /// Daylight is WARM. Godot's original sun here was a blue lamp, which
        /// against a blue ambient and a navy sky produced exactly the flat cold
        /// "engine default" look. Real midday sun lands slightly warm of white;
        /// the BLUE in an outdoor scene belongs to the sky filling the shadows.
        /// Warm key against cool fill is most of what reads as real lighting.
        /// </summary>
        public static readonly Color SunColour = new Color(1.0f, 0.955f, 0.885f);

        public static readonly Color SunsetSunColour = new Color(1.0f, 0.55f, 0.32f);
        public static readonly Color MoonColour = new Color(0.62f, 0.72f, 1.0f);

        /// <summary>
        /// The massing scene's own tuned value, kept exactly. It was 1.7 once,
        /// and with no tonemapping in the pipeline anything much above 1 drives
        /// every sunlit face to white and the concrete reads as painted render.
        /// </summary>
        public const float SunIntensityNoon = 1.15f;

        /// <summary>
        /// Real moonlight is about a millionth of sunlight, which is far too
        /// dim to see by. This is a legibility number, not a physical one.
        /// </summary>
        public const float MoonIntensityPeak = 0.13f;

        /// <summary>
        /// Keeps the golden hour actually lit. A straight sin() falls off so
        /// fast that the interesting half-hour either side of the horizon is
        /// already dark by the time the sun looks low.
        /// </summary>
        public const float ElevationFalloffExponent = 0.55f;

        /// <summary>
        /// How wide, in sine-of-elevation, the sunset glow reaches either side
        /// of the horizon. It peaks at BOTH crossings, because sunrise and
        /// sunset are the same physical moment in opposite directions.
        /// </summary>
        public const float SunsetHalfWidth = 0.3f;

        // --- ambient. The DAY values are the massing scene's own, unchanged,
        // --- because that is the look already tuned and already seen. Night is
        // --- the same fill taken down and shifted blue; the cycle is built
        // --- around the tuned appearance rather than replacing it.

        public static readonly Color AmbientSkyDay = new Color(0.52f, 0.58f, 0.66f);
        public static readonly Color AmbientEquatorDay = new Color(0.38f, 0.40f, 0.44f);
        public static readonly Color AmbientGroundDay = new Color(0.20f, 0.20f, 0.21f);

        // BROUGHT BACK DOWN, as the stopgap that raised them said it should be.
        // These were lifted well above a real night because the street was
        // unreadable without lit windows -- silhouettes survived but the road
        // underfoot was flat black. The windows light up now, which is what
        // makes a real night city legible, so the ambient no longer has to
        // carry it and is back to something nearer night.
        public static readonly Color AmbientSkyNight = new Color(0.085f, 0.098f, 0.160f);
        public static readonly Color AmbientEquatorNight = new Color(0.058f, 0.065f, 0.098f);
        public static readonly Color AmbientGroundNight = new Color(0.028f, 0.030f, 0.042f);

        public static readonly Color SunsetTint = new Color(1.0f, 0.4f, 0.16f);

        /// <summary>How far the sunset tint pulls the ambient fill.</summary>
        public const float SunsetAmbientWeight = 0.5f;

        /// <summary>
        /// The glow is concentrated near the horizon, so the overhead sky takes
        /// less of it than the horizon does.
        /// </summary>
        public const float SunsetTopWeight = 0.45f;

        public static readonly Color SkyHorizonDay = new Color(0.64f, 0.76f, 0.91f);
        public static readonly Color SkyHorizonNight = new Color(0.04f, 0.05f, 0.10f);

        /// <summary>The sky at one moment. Everything a renderer needs, nothing else.</summary>
        public readonly struct State
        {
            public readonly float SunPitchDegrees;
            public readonly float MoonPitchDegrees;
            public readonly float SunElevationSine;
            public readonly float MoonElevationSine;

            /// <summary>0 at full night, 1 at full day, smoothed across dusk.</summary>
            public readonly float Daylight;

            /// <summary>1 when the sun sits on the horizon, 0 well clear of it.</summary>
            public readonly float SunsetAmount;

            public readonly Color SunColour;
            public readonly float SunIntensity;
            public readonly Color MoonColour;
            public readonly float MoonIntensity;

            public readonly Color AmbientSky;
            public readonly Color AmbientEquator;
            public readonly Color AmbientGround;

            /// <summary>
            /// Also the camera's background and the fog. They must stay equal:
            /// matching them is the whole trick that hides the streamed world's
            /// loaded-chunk edge, and letting them drift makes that boundary
            /// visible again at every dawn and dusk.
            /// </summary>
            public readonly Color HorizonColour;

            public readonly bool SunCastsShadow;
            public readonly bool MoonCastsShadow;

            public State(float sunPitch, float moonPitch, float sunElevation, float moonElevation,
                float daylight, float sunset, Color sunColour, float sunIntensity,
                Color moonColour, float moonIntensity, Color ambientSky, Color ambientEquator,
                Color ambientGround, Color horizon, bool sunShadow, bool moonShadow)
            {
                SunPitchDegrees = sunPitch;
                MoonPitchDegrees = moonPitch;
                SunElevationSine = sunElevation;
                MoonElevationSine = moonElevation;
                Daylight = daylight;
                SunsetAmount = sunset;
                SunColour = sunColour;
                SunIntensity = sunIntensity;
                MoonColour = moonColour;
                MoonIntensity = moonIntensity;
                AmbientSky = ambientSky;
                AmbientEquator = ambientEquator;
                AmbientGround = ambientGround;
                HorizonColour = horizon;
                SunCastsShadow = sunShadow;
                MoonCastsShadow = moonShadow;
            }

            public bool IsDaytime => SunElevationSine > 0.0f;
        }

        /// <summary>
        /// The sky at <paramref name="seconds"/> into the cycle. Wraps, so any
        /// value is valid including negative ones.
        /// </summary>
        public static State At(float seconds)
        {
            float time = Mathf.Repeat(seconds, CycleSeconds);

            float sunPitch = SunPitchDegrees(time);
            float moonPitch = sunPitch - 180.0f;

            // The sine of a body's elevation, given a rotation whose x is the
            // pitch about this axis, works out to -sin(pitch). Godot's version
            // checked this numerically against eight angles round the circle
            // rather than assuming it, and the same identity holds here.
            float sunElevation = -Mathf.Sin(sunPitch * Mathf.Deg2Rad);
            float moonElevation = -Mathf.Sin(moonPitch * Mathf.Deg2Rad);

            float sunIntensity = SunIntensityNoon *
                Mathf.Pow(Mathf.Clamp01(sunElevation), ElevationFalloffExponent);
            float moonIntensity = MoonIntensityPeak *
                Mathf.Pow(Mathf.Clamp01(moonElevation), ElevationFalloffExponent);

            // Smoothed across a band straddling the horizon, so dusk shifts
            // gradually instead of the whole world's ambient snapping the
            // instant the sun's own intensity reaches zero.
            float daylight = Smoothstep(-0.15f, 0.15f, sunElevation);
            float sunset = 1.0f - Smoothstep(0.0f, SunsetHalfWidth, Mathf.Abs(sunElevation));

            Color sunColour = Color.Lerp(SunColour, SunsetSunColour, sunset);

            Color ambientSky = Color.Lerp(AmbientSkyNight, AmbientSkyDay, daylight);
            Color ambientEquator = Color.Lerp(AmbientEquatorNight, AmbientEquatorDay, daylight);
            Color ambientGround = Color.Lerp(AmbientGroundNight, AmbientGroundDay, daylight);

            float ambientTint = sunset * SunsetAmbientWeight;
            ambientSky = Color.Lerp(ambientSky, SunsetTint, ambientTint);
            ambientEquator = Color.Lerp(ambientEquator, SunsetTint, ambientTint * SunsetTopWeight);

            Color horizon = Color.Lerp(SkyHorizonNight, SkyHorizonDay, daylight);
            horizon = Color.Lerp(horizon, SunsetTint, sunset);

            // ONLY THE BODY ACTUALLY ABOVE THE HORIZON CASTS. A light at zero
            // intensity still runs its shadow pass, so leaving both enabled
            // renders two overlapping shadow directions blended together
            // wherever both are even slightly up. That was a real bug in the
            // Godot build, reported as "there should just be one shadow".
            return new State(sunPitch, moonPitch, sunElevation, moonElevation,
                daylight, sunset, sunColour, sunIntensity, MoonColour, moonIntensity,
                ambientSky, ambientEquator, ambientGround, horizon,
                sunElevation > 0.0f, moonElevation > 0.0f);
        }

        /// <summary>
        /// The sun's pitch. Day and night are different LENGTHS but each is
        /// still half a turn, so the sun crosses the sky slower than it crosses
        /// back under it.
        /// </summary>
        public static float SunPitchDegrees(float time)
        {
            time = Mathf.Repeat(time, CycleSeconds);
            if (time < DaySeconds)
            {
                return -180.0f * (time / DaySeconds);
            }
            return -180.0f - 180.0f * ((time - DaySeconds) / NightSeconds);
        }

        /// <summary>
        /// Hermite smoothstep. Written out rather than using Mathf.SmoothStep,
        /// which clamps differently at its edges and would shift where dusk
        /// starts by a couple of minutes.
        /// </summary>
        private static float Smoothstep(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3.0f - 2.0f * t);
        }
    }
}
