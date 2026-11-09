using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// UNDER THE GROUND THE SKY GIVES NOTHING (docs/SEWERS.md; owner, 2026-11-09: "mostly pitch black aside from lights from encampments").
    /// As the eye goes down a manhole or in at a tunnel's mouth, the sky's light, its reflections and the haze fade out
    /// (<see cref="DayNightCycle.Underground"/>), and come back as it comes out. One for the whole game, as the lamp directors are.
    /// </summary>
    public sealed class SewerAir : MonoBehaviour
    {
        /// <summary>How fast the dark comes on and lifts, per second: an eye takes a moment.</summary>
        public const float FadePerSecond = 2.5f;

        private static SewerAir _instance;

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying)
            {
                return;
            }
            var host = new GameObject("SewerAir") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<SewerAir>();
        }

        private void LateUpdate()
        {
            Camera eye = Camera.main;
            float dark = eye != null ? CurrentSewers.DarknessAt(eye.transform.position) : 0.0f;
            DayNightCycle.Underground = Mathf.MoveTowards(DayNightCycle.Underground, dark, FadePerSecond * Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            DayNightCycle.Underground = 0.0f;
        }
    }
}
