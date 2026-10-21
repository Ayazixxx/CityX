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
        // A paler blue than it was (0.62, 0.72, 1.0): moonlight reads as cold, not as a blue floodlight (2026-10-09 lighting pass).
        public static readonly Color MoonColour = new Color(0.70f, 0.78f, 1.0f);

        /// <summary>
        /// Godot's own SUN_ENERGY, restored.
        ///
        /// This sat at 1.15 for one stated reason: with no tonemapping in the
        /// pipeline, anything much above 1 drove every sunlit face to flat
        /// white and the concrete read as painted render. That reason is gone
        /// -- the pipeline tonemaps now -- and the cap was costing the thing it
        /// was protecting. A key light barely above its own fill cannot make
        /// contrast, and a city with no contrast is the flat look this pass is
        /// about. Values above 1 are meaningful again: the renderer works in
        /// HDR and the curve brings the highlights back down with a roll-off
        /// instead of clipping them.
        /// </summary>
        public const float SunIntensityNoon = 2.4f;

        /// <summary>
        /// Real moonlight is about a millionth of sunlight, which is far too
        /// dim to see by. This is a legibility number, not a physical one.
        /// (2026-10-09: raised to 0.3 on the research line that night should "feel dark without being dark", and rejected by the owner the same
        /// day -- a moonlit grey-blue city: "at nighttime it needs to be darker", "it should be almost pitch black at night like minecraft". Now
        /// only the faintest outline; the light at night is the lit rooms and the lamps over powered doors.)
        /// </summary>
        public const float MoonIntensityPeak = 0.06f;

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

        // --- ambient, now Godot's own numbers rather than the port's.
        // ---
        // --- THIS IS MOST OF WHY THE TWO BUILDS LOOKED DIFFERENT. The port
        // --- filled the world with a bright, near-grey ambient about three
        // --- times Godot's, and day_night_cycle.gd names precisely what that
        // --- does: a high uniform ambient floods every crevice and erases the
        // --- shading gradient that makes a surface read as solid. Flat, in
        // --- other words -- and a city of plain concrete boxes has less
        // --- tolerance for that than almost anything else would.
        // ---
        // --- Godot's fill is a fifth as bright and properly BLUE, because
        // --- outdoors that is physically what ambient light IS: the sky dome
        // --- filling in everything the sun does not reach directly. The sun
        // --- does the lighting; this only fills. Warm key against cool fill is
        // --- most of what reads as real lighting at all.
        // ---
        // --- COLOUR AND ENERGY STAY APART, as Godot keeps them, because they
        // --- are tuned against each other: the blue is strong and the
        // --- brightness is low, and multiplying them together into one colour
        // --- would hide that and invite somebody to "fix" the dimness later.

        public static readonly Color AmbientDayColour = new Color(0.45f, 0.58f, 0.85f);
        public const float AmbientDayEnergy = 0.3f;

        public static readonly Color AmbientNightColour = new Color(0.12f, 0.15f, 0.28f);

        /// <summary>
        /// Almost nothing (0.09 until 2026-10-09). Owner: night "should be almost pitch black at night like minecraft" -- away from a real light
        /// (a lit room, a lamp over a door) the city is near black, and the 0.09 fill read as a grey-blue wash over everything.
        /// </summary>
        public const float AmbientNightEnergy = 0.65f;   // a gamma colour: what the city gets is about its square, a faint glow

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

        // --- the dome itself. Godot's own day_night_cycle.gd drives all of
        // --- this every frame and the port had none of it, because the port
        // --- had no sky at all -- just a flat clear colour behind the city.

        /// <summary>
        /// Saturated overhead, pale toward the horizon. That gradient is
        /// aerial perspective: more atmosphere between you and the horizon
        /// scatters more light back, and it is what stops a sky reading as a
        /// painted backdrop.
        /// </summary>
        public static readonly Color SkyTopDay = new Color(0.22f, 0.42f, 0.82f);

        public static readonly Color SkyTopNight = new Color(0.015f, 0.02f, 0.05f);
        public static readonly Color GroundBottomDay = new Color(0.16f, 0.16f, 0.18f);
        public static readonly Color GroundBottomNight = new Color(0.015f, 0.015f, 0.03f);

        /// <summary>
        /// The dome's BRIGHTNESS, separate from its colour, and dropped hard
        /// at night. Two reasons, both Godot's own: a night sky sitting at
        /// daytime luminance survives the tonemap curve and stays visibly
        /// bright, and -- since the sky is what the world reflects -- it would
        /// go on lighting the city like an overcast afternoon at midnight.
        /// </summary>
        public const float SkyEnergyDay = 1.0f;

        /// <summary>
        /// The night dome's brightness (0.15 until 2026-10-09). Low enough that the sky is near black and, since the haze takes its colour from
        /// the sky, that the far city does not fade into a grey veil at night ("the greyish hue that appears when you distance yourself from
        /// light"). The stars and the moon's disc have their own brightness and still show.
        /// </summary>
        public const float SkyEnergyNight = 0.05f;

        /// <summary>The ground half of the dome is dimmer than the sky half.</summary>
        public const float GroundEnergyRatio = 0.6f;

        /// <summary>
        /// How wide the sun's disc is, in degrees. Not the real 0.53: a disc
        /// that small is a couple of pixels and reads as a stuck highlight
        /// rather than as the sun.
        /// </summary>
        public const float SunAngularRadius = 1.0f;

        /// <summary>
        /// The moon is about the same apparent size; a touch wider only
        /// because its light is dim enough that a soft edge never gets noisy.
        /// </summary>
        public const float MoonAngularRadius = 1.5f;

        /// <summary>
        /// Below this elevation, a light stops casting shadows instead of
        /// stretching them to extreme length across the street.
        ///
        /// A caster's shadow reaches roughly height/tan(elevation) along the
        /// ground -- for a ~2m-tall character that is already 5-6m at this
        /// threshold, and it keeps growing without bound as elevation
        /// approaches zero. Past a certain length that shadow's far end
        /// reaches nearby building facades, and from most third-person
        /// camera angles a long, near-horizontal ground shadow foreshortens
        /// against a vertical wall behind it and reads as though it were
        /// cast ONTO the wall, floating -- correct geometry, but it looks
        /// exactly like a broken shadow. Cutting shadows off before the sun
        /// or moon gets that low avoids it, the same trade every game with
        /// a day/night cycle makes rather than rendering true sunrise/
        /// sunset-length shadows. Costs about 5-6 minutes of shadowless
        /// dawn/dusk out of the 34-minute cycle, which is not something a
        /// player is likely to consciously notice.
        /// </summary>
        public const float MinShadowElevationSine = 0.35f; // ~20.5 degrees

        /// <summary>
        /// How far the glow around a body reaches, in degrees. Reported
        /// directly as "the sun is too big" against the original 30 --
        /// wide enough, combined with GlowCurve's own inverted exponent (see
        /// its own doc comment), to wash a good third of the sky rather than
        /// read as a sun with a soft corona around it.
        /// </summary>
        public const float GlowRadiusDegrees = 6.0f;

        /// <summary>
        /// The glow's falloff exponent is the reciprocal of this -- CONFIRMED
        /// BACKWARDS FROM ITS OWN ORIGINAL COMMENT, not just retuned: with
        /// the exponent n = 1/GlowCurve, (1 - t^n) sits close to 1 (full
        /// brightness) for MOST of t in [0,1) whenever n is large, i.e.
        /// whenever GlowCurve is SMALL, and only drops sharply right at the
        /// outer edge -- a flat plateau then a cliff, not a glow tight
        /// against the disc. The old value (0.08, n=12.5) is exactly that
        /// plateau, which combined with GlowRadiusDegrees' own old 30 is
        /// what actually washed a third of the sky the original comment said
        /// this exponent was there to prevent. 1.0 is this shader's own
        /// Range(0.001, 1) ceiling -- the largest GlowCurve, smallest
        /// exponent, and so the tightest falloff this parameterisation can
        /// reach: a straight linear fade from the disc's own edge to
        /// GlowRadiusDegrees, not the old near-flat plateau.
        /// </summary>
        public const float GlowCurve = 1.0f;

        /// <summary>city.tscn's own sky_curve / ground_curve.</summary>
        public const float SkyGradientCurve = 0.09f;

        public const float GroundGradientCurve = 0.02f;

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

            /// <summary>
            /// The fill light, already multiplied by its energy -- one value a
            /// renderer can take straight. Uniform, as Godot's is: a real sky
            /// dome is not uniform, but the tuning that stops this washing the
            /// city flat was done against a uniform one.
            /// </summary>
            public readonly Color AmbientColour;

            /// <summary>
            /// Also the camera's background and the fog. They must stay equal:
            /// matching them is the whole trick that hides the streamed world's
            /// loaded-chunk edge, and letting them drift makes that boundary
            /// visible again at every dawn and dusk.
            /// </summary>
            public readonly Color HorizonColour;

            /// <summary>The dome overhead. Its horizon end is HorizonColour.</summary>
            public readonly Color SkyTopColour;

            /// <summary>The dome underneath, which is what a rooftop reflects.</summary>
            public readonly Color GroundBottomColour;

            public readonly float SkyEnergy;
            public readonly float GroundEnergy;

            public readonly bool SunCastsShadow;
            public readonly bool MoonCastsShadow;

            public State(float sunPitch, float moonPitch, float sunElevation, float moonElevation,
                float daylight, float sunset, Color sunColour, float sunIntensity,
                Color moonColour, float moonIntensity, Color ambient,
                Color horizon, Color skyTop, Color groundBottom,
                float skyEnergy, float groundEnergy, bool sunShadow, bool moonShadow)
            {
                SkyTopColour = skyTop;
                GroundBottomColour = groundBottom;
                SkyEnergy = skyEnergy;
                GroundEnergy = groundEnergy;
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
                AmbientColour = ambient;
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

            Color ambient = Color.Lerp(AmbientNightColour, AmbientDayColour, daylight);
            ambient = Color.Lerp(ambient, SunsetTint, sunset * SunsetAmbientWeight);
            ambient = Dim(ambient, Mathf.Lerp(AmbientNightEnergy, AmbientDayEnergy, daylight));

            Color horizon = Color.Lerp(SkyHorizonNight, SkyHorizonDay, daylight);
            horizon = Color.Lerp(horizon, SunsetTint, sunset);

            // The zenith takes LESS of the sunset than the horizon does, by the
            // same weight the ambient uses. A real sunset is a band low in the
            // sky; tinting the whole dome evenly reads as a colour filter over
            // the world rather than as a sun going down.
            Color skyTop = Color.Lerp(SkyTopNight, SkyTopDay, daylight);
            skyTop = Color.Lerp(skyTop, SunsetTint, sunset * SunsetTopWeight);

            Color groundBottom = Color.Lerp(GroundBottomNight, GroundBottomDay, daylight);
            groundBottom = Color.Lerp(groundBottom, SunsetTint, sunset * SunsetTopWeight);

            float skyEnergy = Mathf.Lerp(SkyEnergyNight, SkyEnergyDay, daylight);
            float groundEnergy =
                Mathf.Lerp(SkyEnergyNight, SkyEnergyDay * GroundEnergyRatio, daylight);

            // ONLY THE BODY ACTUALLY ABOVE THE HORIZON CASTS. A light at zero
            // intensity still runs its shadow pass, so leaving both enabled
            // renders two overlapping shadow directions blended together
            // wherever both are even slightly up. That was a real bug in the
            // Godot build, reported as "there should just be one shadow".
            return new State(sunPitch, moonPitch, sunElevation, moonElevation,
                daylight, sunset, sunColour, sunIntensity, MoonColour, moonIntensity,
                ambient, horizon, skyTop, groundBottom, skyEnergy, groundEnergy,
                sunElevation > MinShadowElevationSine, moonElevation > MinShadowElevationSine);
        }

        /// <summary>How much of its luminance a fully overcast sky keeps: a storm's cloud deck is a dark lid, not a bright one.</summary>
        public const float OvercastBrightness = 0.6f;

        /// <summary>The blue-grey of a storm cloud deck, as multipliers on the sky's own grey.</summary>
        public static readonly Color OvercastTint = new Color(0.96f, 1.0f, 1.08f);

        /// <summary>What a lightning flash turns the whole sky toward: a cold near-white.</summary>
        public static readonly Color LightningSkyColour = new Color(0.86f, 0.91f, 1.0f);

        /// <summary>How far a full-strength flash drags the sky toward <see cref="LightningSkyColour"/>.</summary>
        public const float LightningSkyWeight = 0.7f;

        /// <summary>
        /// A sky colour under weather. <paramref name="overcast"/> (0..1) drains
        /// its colour and brightness toward a dark blue-grey -- the clear blue,
        /// the orange of a sunset and the black-blue of a night all go to the
        /// same cloud lid, which is what cloud cover does. <paramref name="flash"/>
        /// (0..1) then throws it toward near-white for a lightning strike.
        /// Both 0 returns the colour untouched, so clear weather is exactly
        /// what SkyCycle computed.
        /// </summary>
        public static Color Weathered(Color colour, float overcast, float flash)
        {
            overcast = Mathf.Clamp01(overcast);
            flash = Mathf.Clamp01(flash);
            if (overcast <= 0.0f && flash <= 0.0f)
            {
                return colour;
            }

            float grey = colour.grayscale * OvercastBrightness;
            var cloud = new Color(grey * OvercastTint.r, grey * OvercastTint.g, grey * OvercastTint.b, colour.a);
            Color result = Color.Lerp(colour, cloud, overcast);
            return Color.Lerp(result, LightningSkyColour, flash * LightningSkyWeight);
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
        /// A colour turned down by an energy multiplier, IN LINEAR SPACE.
        ///
        /// This is not fussiness. Brightness is a property of light, and light
        /// adds and scales linearly; an sRGB triple is a storage encoding with
        /// a curve baked into it, and multiplying that curve by 0.3 is not the
        /// same thing as letting through 30% of the light. Godot scales the
        /// linearised colour, so doing it the other way here landed the fill
        /// about three times too dark -- the first render of this pass came
        /// back with every shadowed facade crushed to near-black silhouette,
        /// and this was why.
        /// </summary>
        public static Color Dim(Color colour, float energy)
        {
            Color scaled = colour.linear * energy;
            scaled.a = 1.0f;
            return scaled.gamma;
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
