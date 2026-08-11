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
        }
    }
}
