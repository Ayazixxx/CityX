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

        [Tooltip("The facade material. Its windows are lit at night and dark by day.")]
        public Material Facades;

        /// <summary>
        /// How bright a lit window burns once night has fully fallen. Above 1
        /// because the emission map's own colour is already a dim warm tint,
        /// and a window has to read as a light source rather than as slightly
        /// paler concrete.
        /// </summary>
        public const float WindowGlow = 2.4f;

        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Seconds into the cycle. A full day and night is 34 minutes.")]
        public float TimeOfDay = SkyCycle.StartSeconds;

        [Tooltip("How fast time runs. 1 is real time; raise it to inspect dusk without waiting.")]
        public float TimeScale = 1.0f;

        [Tooltip("Off freezes the sky wherever TimeOfDay currently is.")]
        public bool Running = true;

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
                Sun.transform.rotation =
                    Quaternion.Euler(state.SunPitchDegrees, SkyCycle.AzimuthDegrees, 0.0f);
                Sun.color = state.SunColour;
                Sun.intensity = state.SunIntensity;
                Sun.shadows = state.SunCastsShadow ? LightShadows.Soft : LightShadows.None;
            }

            if (Moon != null)
            {
                Moon.transform.rotation =
                    Quaternion.Euler(state.MoonPitchDegrees, SkyCycle.AzimuthDegrees, 0.0f);
                Moon.color = state.MoonColour;
                Moon.intensity = state.MoonIntensity;
                Moon.shadows = state.MoonCastsShadow ? LightShadows.Soft : LightShadows.None;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = state.AmbientSky;
            RenderSettings.ambientEquatorColor = state.AmbientEquator;
            RenderSettings.ambientGroundColor = state.AmbientGround;

            RenderSettings.fogColor = state.HorizonColour;

            if (Sky != null)
            {
                // Must match the fog exactly. The streamed world ends at a hard
                // edge, and the only thing hiding it is fog the same colour as
                // what is behind it.
                Sky.backgroundColor = state.HorizonColour;
            }

            if (Facades != null)
            {
                // Windows come on as the light goes, and are fully out by day.
                // Driven off daylight rather than a clock so they light through
                // dusk at the same rate the world darkens, instead of snapping
                // on at some threshold.
                float glow = (1.0f - state.Daylight) * WindowGlow;
                Facades.SetColor(EmissionColour, Color.white * glow);
            }
        }
    }
}
