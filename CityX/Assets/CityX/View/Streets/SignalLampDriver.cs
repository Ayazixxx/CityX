using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Tells the traffic lights' shader (CityX/SignalLamp) the time (docs/TRAFFIC_CONTROL.md): the shared clock every light is a function of, whether it is the dead of
    /// night, when a side street's lights go to flashing, and how plainly a lit lens glows. One for the whole game, made when the first lights are.
    /// </summary>
    public sealed class SignalLampDriver : MonoBehaviour
    {
        /// <summary>The sun this far under the horizon (the sine of its height) is the dead of night: the middle third of it.</summary>
        public const float FlashingBelowSun = -0.55f;

        /// <summary>A lit lens's glow, in full sun and in the dark.</summary>
        public const float GlowByDay = 0.12f, GlowByNight = 0.55f;

        private static readonly int ClockId = Shader.PropertyToID("_CityXSignalClock");
        private static readonly int NightId = Shader.PropertyToID("_CityXSignalNight");
        private static readonly int GlowId = Shader.PropertyToID("_CityXSignalGlow");

        private static SignalLampDriver _instance;

        /// <summary>Whether the lights are in late-night flashing operation on this machine.</summary>
        public static bool Flashing { get; private set; }

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying)
            {
                return;
            }
            var host = new GameObject("SignalLampDriver") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SignalLampDriver>();
        }

        private void Update()
        {
            SignalClock.Local = Time.timeAsDouble;
            Flashing = CurrentSky.TowardSun.y < FlashingBelowSun;
            Shader.SetGlobalFloat(ClockId, (float)SignalTiming.Mod(SignalClock.Seconds, SignalTiming.ClockWrap));
            Shader.SetGlobalFloat(NightId, Flashing ? 1.0f : 0.0f);
            Shader.SetGlobalFloat(GlowId, Mathf.Lerp(GlowByNight, GlowByDay, Mathf.Clamp01(CurrentSky.Sunshine)));
        }
    }
}
