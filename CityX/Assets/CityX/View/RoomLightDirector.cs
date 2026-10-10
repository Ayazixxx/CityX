using System.Collections.Generic;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Which lit rooms (RoomLightGroup) get real lights, and which of their lamps cast shadows. A lit room's glow (fittings, windows) is always shown;
    /// its lamps and bounces cost light in every pixel near them, so only the lit rooms within <see cref="LightReachMetres"/> of the camera keep
    /// them, nearest first, up to <see cref="MaxRoomLights"/> in all (URP draws a limited number of lights at once, and one lit tower has hundreds).
    /// The <see cref="MaxShadowedLamps"/> lamps nearest the camera, within <see cref="ShadowReachMetres"/>, cast soft shadows (2026-10-09 research:
    /// shadows only where the player is, never everywhere): bodies, stairs and partitions throw shadows in the room you are in, and the wall between
    /// two rooms stops the light of one leaking into the other.
    ///
    /// Made on demand by the first lit room (hidden, kept across scenes). Every <see cref="Interval"/> seconds, from LateUpdate.
    /// </summary>
    public sealed class RoomLightDirector : MonoBehaviour
    {
        /// <summary>Lit rooms further than this from the camera are their glow alone.</summary>
        public const float LightReachMetres = 110.0f;

        /// <summary>The most room lights (lamps and bounces) on at once.</summary>
        public const int MaxRoomLights = 160;

        /// <summary>Lamps further than this from the camera never cast shadows.</summary>
        public const float ShadowReachMetres = 30.0f;

        /// <summary>The most lamps casting shadows at once (a spot's shadow is one extra render of what it sees).</summary>
        public const int MaxShadowedLamps = 4;

        /// <summary>How often the choice is made again, seconds.</summary>
        public const float Interval = 0.2f;

        private static RoomLightDirector _instance;
        private static readonly List<RoomLightGroup> Order = new List<RoomLightGroup>();
        private static readonly List<KeyValuePair<float, Light>> Shadowable = new List<KeyValuePair<float, Light>>();
        private float _wait;

        /// <summary>Makes the director if there is none (in Play mode only).</summary>
        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying)
            {
                return;
            }
            var go = new GameObject("RoomLightDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RoomLightDirector>();
        }

        private void LateUpdate()
        {
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0.0f)
            {
                return;
            }
            _wait = Interval;
            Camera camera = Camera.main;
            Direct(camera != null ? camera.transform.position : (Vector3?)null);
        }

        /// <summary>
        /// Decides, for every lit room, whether its lights are on and which lamps cast shadows, as seen from <paramref name="eye"/> (null: no camera;
        /// every lit room keeps its lights up to the budget, and nothing casts a shadow). Public for tests.
        /// </summary>
        public static void Direct(Vector3? eye)
        {
            Order.Clear();
            foreach (RoomLightGroup group in RoomLightGroup.Lit)
            {
                if (group != null)
                {
                    Order.Add(group);
                }
            }
            if (eye.HasValue)
            {
                Vector3 e = eye.Value;
                Order.Sort((a, b) => (a.Centre - e).sqrMagnitude.CompareTo((b.Centre - e).sqrMagnitude));
            }

            int budget = MaxRoomLights;
            Shadowable.Clear();
            foreach (RoomLightGroup group in Order)
            {
                bool near = !eye.HasValue || (group.Centre - eye.Value).sqrMagnitude <= LightReachMetres * LightReachMetres;
                bool on = near && group.LightCount <= budget;
                group.SetLights(on);
                if (!on)
                {
                    continue;
                }
                budget -= group.LightCount;
                foreach (Light lamp in group.Lamps)
                {
                    if (lamp == null)
                    {
                        continue;
                    }
                    float distance = eye.HasValue ? Vector3.Distance(lamp.transform.position, eye.Value) : float.MaxValue;
                    Shadowable.Add(new KeyValuePair<float, Light>(distance, lamp));
                }
            }

            // The nearest few lamps in reach cast shadows; every other lamp of a lit room does not.
            Shadowable.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < Shadowable.Count; i++)
            {
                bool shadowed = i < MaxShadowedLamps && Shadowable[i].Key <= ShadowReachMetres;
                LightShadows wanted = shadowed ? LightShadows.Soft : LightShadows.None;
                Light lamp = Shadowable[i].Value;
                if (lamp.shadows != wanted)
                {
                    lamp.shadows = wanted;
                }
            }
        }
    }
}
