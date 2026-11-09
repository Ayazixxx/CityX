using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The city's air (Shaders/CityXHaze.shader, a full-screen pass the scene builder puts on the renderer): exponential height fog, thick in the
    /// street and thinning with height, the colour of the sky in the direction looked and brightened toward the sun. It is what makes distance read
    /// as distance in a big city -- the near street sharp, the towers a few blocks off paler and bluer, the far skyline melting into the sky behind
    /// it (aerial perspective) -- and it replaces the old linear fog, which was a flat grey wall at 150-380 m that hid the end of the streamed city
    /// and nothing else.
    ///
    /// Pure numbers here, pushed into the shader's globals by DayNightCycle every frame. The streamed-city edge still comes from
    /// RenderSettings' fog distances, because the storm (RainEffect) and the intro's cloud fall (IntroCutscene) already drive those.
    /// </summary>
    public static class CityHaze
    {
        /// <summary>Haze per metre of air at street level on a clear day: a tower 100 m away is about a seventh hazed, one 300 m away about a third.</summary>
        public const float ClearDensity = 0.0015f;

        /// <summary>A storm's cloud lid makes the air this many times thicker.</summary>
        public const float OvercastThickening = 2.5f;

        /// <summary>The haze thins by e every this many metres up: the roofs of a 150 m tower stand in clearer air than its feet.</summary>
        public const float ScaleHeight = 150.0f;

        /// <summary>The most the air ever hides of a building inside the streamed city, so a far silhouette still reads against the sky.</summary>
        public const float MaxHaze = 0.85f;

        /// <summary>
        /// The clear bubble round the player (owner, 2026-10-09: "I don't want to be walking around the whole game in fog"): no haze at all closer than
        /// this, metres from the camera...
        /// </summary>
        public const float ClearRadius = 25.0f;

        /// <summary>...fading in gently until the air is at its full thickness this far out. The edge of the streamed city still fades regardless.</summary>
        public const float FadeInEnd = 120.0f;

        /// <summary>The edge fade starts this far between the fog start and the fog end distance (and is total at the end).</summary>
        public const float EdgeFadeFraction = 0.5f;

        /// <summary>How much of the sun's own light the haze looking straight at it adds to the sky's colour (a sunset glow, not a white-out).</summary>
        public const float SunGlow = 0.35f;

        public readonly struct Settings
        {
            /// <summary>The sky at the horizon: the haze looking level, and the colour everything past the streamed city's edge becomes.</summary>
            public readonly Color Colour;

            /// <summary>The haze looking straight at the sun.</summary>
            public readonly Color SunColour;

            /// <summary>The sky overhead: the haze looking up fades toward it the way the dome does (so a far tower fades into the sky behind it).</summary>
            public readonly Color SkyTop;

            public readonly float SkyCurve;
            public readonly float SkyEnergy;
            public readonly float Density;
            public readonly float Falloff;
            public readonly float BaseY;
            public readonly float EdgeStart;
            public readonly float EdgeEnd;

            public Settings(Color colour, Color sunColour, Color skyTop, float skyCurve, float skyEnergy, float density, float falloff, float baseY,
                float edgeStart, float edgeEnd)
            {
                Colour = colour;
                SunColour = sunColour;
                SkyTop = skyTop;
                SkyCurve = skyCurve;
                SkyEnergy = skyEnergy;
                Density = density;
                Falloff = falloff;
                BaseY = baseY;
                EdgeStart = edgeStart;
                EdgeEnd = edgeEnd;
            }
        }

        /// <summary>
        /// The haze for a sky: its horizon and zenith colours (already weathered) and energy, the sun's colour and intensity, how overcast it is, how
        /// much of the sun the clouds let through, and the fog distances the edge of the streamed city is hidden by.
        /// </summary>
        public static Settings For(Color horizon, Color skyTop, float skyEnergy, Color sunColour, float sunIntensity, float overcast, float sunVisible,
            float fogStart, float fogEnd)
        {
            float density = ClearDensity * (1.0f + OvercastThickening * Mathf.Clamp01(overcast));
            Color glow = horizon + sunColour * (Mathf.Max(0.0f, sunIntensity) * SunGlow * Mathf.Clamp01(sunVisible));
            glow.a = 1.0f;
            float end = Mathf.Max(1.0f, fogEnd);
            float start = Mathf.Clamp(Mathf.Lerp(fogStart, end, EdgeFadeFraction), 0.0f, end - 0.5f);
            return new Settings(horizon, glow, skyTop, SkyCycle.SkyGradientCurve, skyEnergy, density, 1.0f / ScaleHeight, 0.0f, start, end);
        }

        private static readonly int ColourId = Shader.PropertyToID("_CityXHazeColor");
        private static readonly int SunColourId = Shader.PropertyToID("_CityXHazeSunColor");
        private static readonly int SunDirId = Shader.PropertyToID("_CityXHazeSunDir");
        private static readonly int ParamsId = Shader.PropertyToID("_CityXHazeParams");
        private static readonly int EdgeId = Shader.PropertyToID("_CityXHazeEdge");
        private static readonly int SkyTopId = Shader.PropertyToID("_CityXHazeSkyTop");
        private static readonly int SkyId = Shader.PropertyToID("_CityXHazeSky");

        /// <summary>
        /// Pushes the haze into the shader globals; <paramref name="towardSun"/> is the unit direction to the sun. <paramref name="thin"/> thins
        /// the air itself (none of it in a tunnel) and leaves the fade at the streamed city's edge as it is.
        /// </summary>
        public static void Push(in Settings s, Vector3 towardSun, float thin = 1.0f)
        {
            Shader.SetGlobalColor(ColourId, s.Colour);
            Shader.SetGlobalColor(SunColourId, s.SunColour);
            Shader.SetGlobalColor(SkyTopId, s.SkyTop);
            Shader.SetGlobalVector(SkyId, new Vector4(s.SkyCurve, s.SkyEnergy, 0.0f, 0.0f));
            Shader.SetGlobalVector(SunDirId, towardSun);
            Shader.SetGlobalVector(ParamsId, new Vector4(s.Density * Mathf.Clamp01(thin), s.Falloff, s.BaseY, MaxHaze));
            Shader.SetGlobalVector(EdgeId, new Vector4(s.EdgeStart, s.EdgeEnd, ClearRadius, FadeInEnd));
        }

        /// <summary>No haze: the pass leaves the image alone (a scene without a day/night cycle, like the title screen).</summary>
        public static void Clear()
        {
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
            Shader.SetGlobalVector(EdgeId, Vector4.zero);
        }

        /// <summary>The density the shader is currently drawing with (0 = off). For tests.</summary>
        public static float CurrentDensity => Shader.GetGlobalVector(ParamsId).x;
    }
}
