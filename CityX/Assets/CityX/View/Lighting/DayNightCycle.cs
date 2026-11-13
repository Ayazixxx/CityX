using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Applies <see cref="SkyCycle"/> to the scene's lights, ambient and fog.
    ///
    /// Deliberately thin. Every decision lives in SkyCycle as a pure function;
    /// this only pushes the result into the renderer, which is the part that
    /// cannot be tested without a scene.
    /// </summary>
    [ExecuteAlways]
    public sealed class DayNightCycle : MonoBehaviour
    {
        [Tooltip("The sun. Required.")]
        public Light Sun;

        [Tooltip("The moon, 180 degrees behind the sun. Optional.")]
        public Light Moon;

        [Tooltip("Its background follows the horizon so the fog wall is invisible.")]
        public Camera Sky;

        [Tooltip("The sky dome material. Its colours and brightness are driven from here.")]
        public Material SkyDome;

        [Tooltip("The lit-window glass. This is where the city's night light actually comes from.")]
        public Material LitWindows;

        [Tooltip("The night grade (a global Volume over the day grade). Its weight follows the dark: 0 by day, 1 at night. Optional.")]
        public UnityEngine.Rendering.Volume NightGrade;

        /// <summary>
        /// How far out of the sky's light the eye is, 0 to 1 (SewerAir: down a manhole, in a tunnel). The sky's light, its reflections, the
        /// haze and the sunbeams go with it, and on a body the sun, the moon and the city's lamps too (_CityXUnderground in
        /// CityXToonLighting.hlsl): what lights a tunnel is what shines in it.
        /// </summary>
        public static float Underground { get; set; }

        /// <summary>
        /// The trace the dark keeps round the eye under the ground, in linear light (_CityXDarkTrace, CityXToonLighting.hlsl): what a surface
        /// at the eye and facing it gets, before its own colour. The owner asked for "pitch black like night time in minecraft with barely
        /// any visibility" (2026-11-09), and the games that are truly dark all keep this much, so the wall in front of the player is a shape
        /// and not a guess. On the tunnels' grey brick, which gives back an eighth of what falls on it, it shows the wall beside the player
        /// about a twentieth of the way from black to white and nothing a few steps on: nothing a flashlight would not be wanted for.
        /// </summary>
        public static readonly Color DarkTrace = new Color(0.16f, 0.19f, 0.25f);

        /// <summary>How far from the eye the trace reaches, in metres. Beyond it the dark is black.</summary>
        public const float DarkTraceReach = 14.0f;

        /// <summary>
        /// How much of the sky's light the inside of a building does NOT get (the facade shader's _CityXIndoorShade, on every surface marked
        /// indoors -- SurfaceLook): by day a room is lit by its windows, not by the whole open sky the street sees; at night the sky is dark anyway,
        /// and a room is what its own lamps make it (2026-10-09 lighting pass).
        /// </summary>
        public const float IndoorShadeDay = 0.65f;
        public const float IndoorShadeNight = 0.92f;   // 0.5 until the owner asked for pitch-black nights: a dark room is dark

        /// <summary>How bright the stars are on a clear night (CityXProceduralSky's _StarEnergy).</summary>
        public const float StarBrightness = 1.5f;

        /// <summary>How much of the sky's reflection is left at night (glass, wet streets): almost none.</summary>
        public const float NightReflection = 0.03f;

        /// <summary>How much daylight there is now (0 night .. 1 day), as last applied: what the lamps over powered doors go by (EntranceLamp). 1 when
        /// nothing has applied a sky, so a scene without a day/night cycle has them off. Settable for tests.</summary>
        public static float Daylight { get; set; } = 1.0f;

        private static readonly int IndoorShade = Shader.PropertyToID("_CityXIndoorShade");
        private static readonly int EyeUnderground = Shader.PropertyToID("_CityXUnderground");
        private static readonly int DarkTraceLight = Shader.PropertyToID("_CityXDarkTrace");
        private static readonly int StarEnergy = Shader.PropertyToID("_StarEnergy");

        /// <summary>
        /// How bright a lit window burns once night has fully fallen. Above 1
        /// because the emission map's own colour is already a dim warm tint,
        /// and a window has to read as a light source rather than as slightly
        /// paler concrete.
        /// </summary>
        public const float WindowGlow = 2.4f;

        /// <summary>
        /// title_screen_city.gd's WINDOW_LIGHT_COLOR. Warm tungsten, which is
        /// what makes a night skyline read as inhabited rather than as a wall
        /// with holes in it. It used to live in the facade emission map; now
        /// that the windows are real glass it lives here.
        /// </summary>
        public static readonly Color LitWindowColour = new Color(0.988f, 0.796f, 0.443f);

        /// <summary>
        /// How much brighter the moon's DISC is than the light it casts. Its
        /// intensity is a legibility number for lighting a street by, and a
        /// disc drawn at that value is an almost invisible smudge.
        /// </summary>
        public const float MoonDiscGain = 20.0f;   // 6 until the moon's light went down to almost nothing: the disc stays a bright moon

        /// <summary>How much of the sun's and moon's disc a full overcast hides (1 would hide it completely).</summary>
        public const float OvercastDiscHiding = 0.95f;

        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        private static readonly int SkyTop = Shader.PropertyToID("_SkyTopColour");
        private static readonly int SkyHorizon = Shader.PropertyToID("_SkyHorizonColour");
        private static readonly int SkyEnergy = Shader.PropertyToID("_SkyEnergy");
        private static readonly int GroundBottom = Shader.PropertyToID("_GroundBottomColour");
        private static readonly int GroundHorizon = Shader.PropertyToID("_GroundHorizonColour");
        private static readonly int GroundEnergy = Shader.PropertyToID("_GroundEnergy");
        private static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int SunColour = Shader.PropertyToID("_SunColour");
        private static readonly int SunEnergy = Shader.PropertyToID("_SunEnergy");
        private static readonly int MoonDirection = Shader.PropertyToID("_MoonDirection");
        private static readonly int MoonColour = Shader.PropertyToID("_MoonColour");
        private static readonly int MoonEnergy = Shader.PropertyToID("_MoonEnergy");

        [Tooltip("Seconds into the cycle. A full day and night is 34 minutes.")]
        public float TimeOfDay = SkyCycle.StartSeconds;

        [Tooltip("How fast time runs. 1 is real time; raise it to inspect dusk without waiting.")]
        public float TimeScale = 1.0f;

        [Tooltip("Off freezes the sky wherever TimeOfDay currently is.")]
        public bool Running = true;

        /// <summary>
        /// 0 clear .. 1 a storm's cloud lid. Set by RainEffect; the sky, the
        /// fog and the camera background all go grey together, which is the
        /// same "dome horizon IS the fog colour" rule Apply already keeps.
        /// </summary>
        [Range(0.0f, 1.0f)] public float Overcast;

        /// <summary>0..1 lightning flash over the whole sky. Set by RainEffect.</summary>
        [Range(0.0f, 1.0f)] public float Flash;

        private void Update()
        {
            if (Running && Application.isPlaying)
            {
                TimeOfDay = Mathf.Repeat(
                    TimeOfDay + Time.deltaTime * TimeScale, SkyCycle.CycleSeconds);
            }
            Apply(SkyCycle.At(TimeOfDay));

            // The sky changes slowly, so its reflections and ambient are re-captured every few seconds, not every frame.
            if (Application.isPlaying)
            {
                _sinceEnvironmentUpdate += Time.unscaledDeltaTime;
                if (_sinceEnvironmentUpdate >= EnvironmentUpdateSeconds)
                {
                    _sinceEnvironmentUpdate = 0.0f;
                    DynamicGI.UpdateEnvironment();
                }
            }
        }

        /// <summary>How often the sky is re-captured into the reflection probe (shiny glass, wet streets, car paint reflect the real sky).</summary>
        public const float EnvironmentUpdateSeconds = 3.0f;

        private float _sinceEnvironmentUpdate = 99.0f;

        [Tooltip("Off goes back to the built-in linear fog instead of the haze pass (for a renderer without the CityXHaze feature).")]
        public bool HazeDrawn = true;

        private void OnDisable()
        {
            // Shader globals outlive the scene: a scene without a sky cycle (the title screen) must not inherit this one's air, or its dark.
            CityHaze.Clear();
            CurrentSky.Clear();
            Shader.SetGlobalFloat(EyeUnderground, 0.0f);
        }

        /// <summary>
        /// Public so an editor tool can pose the sky at an arbitrary moment
        /// without running the game -- which is the only way to look at dusk
        /// without waiting seventeen minutes for it.
        /// </summary>
        public void Apply(SkyCycle.State state)
        {
            if (Sun != null)
            {
                // NEGATED. SkyCycle's own pitch is the angle that makes
                // -sin(pitch) equal the body's elevation (verified against
                // Godot numerically -- see SkyCycle.At's own comment), but a
                // Unity Directional Light shines along its OWN forward axis,
                // and Quaternion.Euler(pitch, ...) applied directly put that
                // forward pointing UP whenever the body was above the
                // horizon -- every roof in the city rendered unlit while the
                // walls caught light, because a light shining upward never
                // reaches an upward-facing surface. Negating the pitch here
                // is the one line that turns "elevation" into "which way the
                // light actually travels" without touching SkyCycle's own
                // math or any test that only ever reads its numbers.
                Sun.transform.rotation =
                    Quaternion.Euler(-state.SunPitchDegrees, SkyCycle.AzimuthDegrees, 0.0f);
                Sun.color = state.SunColour;
                Sun.intensity = state.SunIntensity;
                Sun.shadows = state.SunCastsShadow ? LightShadows.Soft : LightShadows.None;
                CurrentSky.TowardSun = -Sun.transform.forward;
                // The flare is drawn at the sun's place on the screen whatever the sun's brightness, and the ground does not hide it: with the
                // sun down, an eye looking at the street at night saw it as a pale disc lying on the road.
                if (Sun.TryGetComponent(out UnityEngine.Rendering.LensFlareComponentSRP flare))
                {
                    flare.enabled = state.SunElevationSine > 0.0f;
                }
            }
            CurrentSky.Sunshine = CurrentSky.SunshineOf(state.SunElevationSine, Overcast);

            if (Moon != null)
            {
                Moon.transform.rotation =
                    Quaternion.Euler(-state.MoonPitchDegrees, SkyCycle.AzimuthDegrees, 0.0f);
                Moon.color = state.MoonColour;
                Moon.intensity = state.MoonIntensity;
                Moon.shadows = state.MoonCastsShadow ? LightShadows.Soft : LightShadows.None;
            }

            // FLAT, not Trilight, and dim. Godot's ambient is a single colour
            // at a low energy, and SkyCycle's own note explains why that
            // matters more than the shape of the fill does.
            // (Was a single flat colour. URP has no ray-traced or baked GI to give surfaces a sense of where the light comes from, so the
            // cheap honest stand-in is a TRILIGHT ambient: more and bluer light from the open sky above, the base colour at the horizon, and a
            // darker, warmer bounce off the street from below. Same average brightness as the old flat one, but up-facing roofs and
            // down-facing undersides, and the two sides of a street canyon, no longer light identically.)
            float underground = Mathf.Clamp01(Underground);
            float aboveGround = 1.0f - underground;
            Shader.SetGlobalFloat(EyeUnderground, underground);
            Shader.SetGlobalVector(DarkTraceLight, new Vector4(DarkTrace.r, DarkTrace.g, DarkTrace.b, DarkTraceReach));
            Color baseAmbient = state.AmbientColour * aboveGround;
            Color skyTint = SkyCycle.Weathered(state.SkyTopColour, Overcast, Flash);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(baseAmbient, skyTint * baseAmbient.maxColorComponent * 2.0f, 0.4f) * 1.35f;
            RenderSettings.ambientEquatorColor = baseAmbient;
            RenderSettings.ambientGroundColor = Color.Lerp(baseAmbient, new Color(0.45f, 0.38f, 0.3f) * baseAmbient.maxColorComponent * 2.0f, 0.5f) * 0.5f;

            // Reflections come from the real sky dome, captured by DynamicGI.UpdateEnvironment (see Update).
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            // Faded with the daylight: the reflection map is the sky as it was captured, and a daytime sky in every window and wet street
            // at midnight was the pale grey glow the owner saw ("get rid of this greyish hue"; night should be "almost pitch black").
            RenderSettings.reflectionIntensity = Mathf.Lerp(NightReflection, 1.0f, state.Daylight) * aboveGround;

            // Weather goes through the sky, the fog and the camera background
            // ALL TOGETHER, from the same colour: the horizon end of the dome
            // IS the fog colour, and a storm that greyed one without the
            // others would put the streamed world's edge back in view.
            Color horizon = SkyCycle.Weathered(state.HorizonColour, Overcast, Flash);
            // Under the ground the dome is black (its shader reads _CityXUnderground), and so is what stands in for it, so a tunnel past the draw
            // distance ends in the dark (owner, 2026-11-12: "you can see the light at the end of the tunnels ... make it pitch back dark in there").
            Color behindEverything = Color.Lerp(horizon, Color.black, underground);

            RenderSettings.fogColor = behindEverything;

            // The air (CityHaze): it does the old linear fog's job of hiding the end of the streamed city, out at the same fog distances the storm
            // and the intro still drive, and it also does what that fog never did -- fade each tower by how far away and how high it is. The
            // built-in fog is switched off so the two don't stack; its colour and distances are still kept, as the haze's inputs.
            if (HazeDrawn)
            {
                Vector3 towardSun = Sun != null ? -Sun.transform.forward : Vector3.up;
                CityHaze.Push(CityHaze.For(horizon, SkyCycle.Weathered(state.SkyTopColour, Overcast, Flash), state.SkyEnergy,
                    state.SunColour, state.SunIntensity, Overcast, 1.0f - Overcast * OvercastDiscHiding,
                    RenderSettings.fogStartDistance, RenderSettings.fogEndDistance), towardSun, aboveGround);
            }
            else
            {
                CityHaze.Clear();
            }
            RenderSettings.fog = !HazeDrawn;

            if (Sky != null)
            {
                // Must match the fog exactly. The streamed world ends at a hard
                // edge, and the only thing hiding it is fog the same colour as
                // what is behind it. Still set even though the camera now
                // clears to the sky dome rather than to this, because the dome
                // meets the horizon at exactly this colour and anything that
                // falls back to a clear colour should land on it too.
                Sky.backgroundColor = behindEverything;
            }

            if (SkyDome != null)
            {
                // The dome's horizon end IS the fog colour, by construction
                // rather than by two values being kept in step -- which is the
                // only way that never drifts.
                SkyDome.SetColor(SkyTop, SkyCycle.Weathered(state.SkyTopColour, Overcast, Flash));
                SkyDome.SetColor(SkyHorizon, horizon);
                SkyDome.SetFloat(SkyEnergy, state.SkyEnergy);
                SkyDome.SetColor(GroundHorizon, horizon);
                SkyDome.SetColor(GroundBottom, SkyCycle.Weathered(state.GroundBottomColour, Overcast, Flash));
                SkyDome.SetFloat(GroundEnergy, state.GroundEnergy);

                // A cloud lid hides the sun and moon: their discs fade out
                // rather than shining through the storm.
                float discVisible = 1.0f - Overcast * OvercastDiscHiding;

                // The discs. Fed the lights' own directions rather than
                // recomputed from the pitch, so the sun in the sky cannot drift
                // out of step with the sun casting the shadows.
                if (Sun != null)
                {
                    SkyDome.SetVector(SunDirection, -Sun.transform.forward);
                    SkyDome.SetColor(SunColour, state.SunColour);
                    SkyDome.SetFloat(SunEnergy, state.SunIntensity * discVisible);
                }
                if (Moon != null)
                {
                    SkyDome.SetVector(MoonDirection, -Moon.transform.forward);
                    SkyDome.SetColor(MoonColour, state.MoonColour);
                    // Scaled up off its own intensity, which is a legibility
                    // number for lighting the street and would leave the moon
                    // itself an almost invisible smudge.
                    SkyDome.SetFloat(MoonEnergy, state.MoonIntensity * MoonDiscGain * discVisible);
                }
            }

            // Windows come on as the light goes, and are fully out by day.
            // Driven off daylight rather than a clock so they light through
            // dusk at the same rate the world darkens, instead of snapping on
            // at some threshold.
            //
            // ONLY THE GLASS. This used to drive the facade material too, back
            // when the windows were painted onto it and an emission MAP picked
            // out which pixels were a window. The windows are real holes with
            // real glass in them now and that map is gone -- so the same call
            // no longer lights the windows, it lights the entire wall. See
            // CityMassingSceneBuilder's EnsureMaterial for the other half of
            // this: a material asset remembers, so the old value had to be
            // cleared as well as stopped.
            float glow = (1.0f - state.Daylight) * WindowGlow;
            if (LitWindows != null)
            {
                LitWindows.SetColor(EmissionColour, LitWindowColour * glow);
            }

            // Inside a building, less of the sky (CityMeshBuilder.Interior.cs marks what is inside).
            Shader.SetGlobalFloat(IndoorShade, Mathf.Lerp(IndoorShadeNight, IndoorShadeDay, state.Daylight));
            Daylight = state.Daylight;

            // The stars come out as the light goes, and a cloud lid hides them.
            if (SkyDome != null)
            {
                float dark = 1.0f - state.Daylight;
                SkyDome.SetFloat(StarEnergy, StarBrightness * dark * dark * (1.0f - Overcast));
            }

            // The night grade: a little brighter, cooler and greyer, the way eyes see in the dark (CityMassingSceneBuilder.EnsureNightGradeProfile).
            if (NightGrade != null)
            {
                NightGrade.weight = 1.0f - state.Daylight;
            }
        }
    }
}
