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
        public const float MoonDiscGain = 6.0f;

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
            }

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
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = state.AmbientColour;

            // Weather goes through the sky, the fog and the camera background
            // ALL TOGETHER, from the same colour: the horizon end of the dome
            // IS the fog colour, and a storm that greyed one without the
            // others would put the streamed world's edge back in view.
            Color horizon = SkyCycle.Weathered(state.HorizonColour, Overcast, Flash);

            RenderSettings.fogColor = horizon;

            if (Sky != null)
            {
                // Must match the fog exactly. The streamed world ends at a hard
                // edge, and the only thing hiding it is fog the same colour as
                // what is behind it. Still set even though the camera now
                // clears to the sky dome rather than to this, because the dome
                // meets the horizon at exactly this colour and anything that
                // falls back to a clear colour should land on it too.
                Sky.backgroundColor = horizon;
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
        }
    }
}
