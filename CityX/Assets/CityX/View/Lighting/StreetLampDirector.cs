using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Where the street lights with power hang, chunk by chunk, as the chunks that stand them say (CitySurfaceBuilder.StreetLights.cs, on
    /// the streamer's worker threads). A chunk is a pure function of the city, so what it says twice is the same; what is kept for a
    /// chunk long gone costs a few numbers and is never asked for (only lamps near the eye are, and those chunks are built).
    /// </summary>
    public static class StreetLampSites
    {
        private static readonly Dictionary<Vector2Int, Vector3[]> ByChunk = new Dictionary<Vector2Int, Vector3[]>();

        /// <summary>A chunk's powered lamps, in the world's own coordinates (none: the chunk is forgotten).</summary>
        public static void Report(int chunkX, int chunkY, List<Vector3> heads)
        {
            lock (ByChunk)
            {
                var key = new Vector2Int(chunkX, chunkY);
                if (heads == null || heads.Count == 0)
                {
                    ByChunk.Remove(key);
                }
                else
                {
                    ByChunk[key] = heads.ToArray();
                }
            }
        }

        /// <summary>The powered lamps within <paramref name="reach"/> metres of a point, added to <paramref name="into"/>.</summary>
        public static void Near(Vector3 eye, float reach, List<Vector3> into)
        {
            float size = (float)PlanWorld.ChunkSize;
            int x0 = Mathf.FloorToInt((eye.x - reach) / size), x1 = Mathf.FloorToInt((eye.x + reach) / size);
            int y0 = Mathf.FloorToInt((eye.z - reach) / size), y1 = Mathf.FloorToInt((eye.z + reach) / size);
            float reachSquared = reach * reach;
            lock (ByChunk)
            {
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (!ByChunk.TryGetValue(new Vector2Int(x, y), out Vector3[] heads))
                        {
                            continue;
                        }
                        foreach (Vector3 head in heads)
                        {
                            if ((head - eye).sqrMagnitude <= reachSquared)
                            {
                                into.Add(head);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Forgets every lamp (a new world; a test).</summary>
        public static void Clear()
        {
            lock (ByChunk)
            {
                ByChunk.Clear();
            }
        }
    }

    /// <summary>
    /// LENDS REAL LIGHT TO THE NEAREST STREET LIGHTS AT NIGHT (docs/AMERICAN_STREETS.md). Every powered lamp's lens glows by itself (it is
    /// drawn with the lit windows); the pool of light under it on the road is a real spot light, and there are far too many lamps in a
    /// city for each to keep one. So a few lights are made once and stood under the lamps nearest the eye, fading out toward the edge of
    /// their reach, so that one changing hands is never seen. One for the whole game, as the room lights' director is.
    /// </summary>
    public sealed class StreetLampDirector : MonoBehaviour
    {
        /// <summary>How many lamps cast real light at once: the nearest ones.</summary>
        public const int MaxLights = 14;

        /// <summary>A lamp this far from the eye casts none, and one nearer than <see cref="FullWithinMetres"/> casts all of its light, metres.</summary>
        public const float ReachMetres = 95.0f;

        public const float FullWithinMetres = 70.0f;

        /// <summary>The lamps come on when the sun is this low (the sine of its height): at dusk, before it is dark.</summary>
        public const float OnBelowSun = 0.06f;

        /// <summary>How often the lights are stood again, seconds.</summary>
        public const float Interval = 0.25f;

        /// <summary>
        /// The light itself: a wide cone down onto the road, the orange of a sodium lamp, bright enough that its pool, which fades smoothly
        /// from under the lamp, still reads across the road.
        /// </summary>
        public const float LightRange = 26.0f, ConeDegrees = 150.0f, InnerConeDegrees = 105.0f, Brightness = 40.0f;

        public static readonly Color Sodium = new Color(1.0f, 0.70f, 0.38f);

        private static StreetLampDirector _instance;
        private static readonly List<Vector3> Lamps = new List<Vector3>();
        private static Vector3 _eye;
        private static readonly System.Comparison<Vector3> NearestFirst = (a, b) => (a - _eye).sqrMagnitude.CompareTo((b - _eye).sqrMagnitude);

        private readonly List<Light> _lights = new List<Light>();
        private float _wait;

        /// <summary>How many lamps cast real light now.</summary>
        public static int Lit { get; private set; }

        /// <summary>Whether the street lights are on at all: it is dusk or night.</summary>
        public static bool IsNight => CurrentSky.TowardSun.y < OnBelowSun;

        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying)
            {
                return;
            }
            var host = new GameObject("StreetLampDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<StreetLampDirector>();
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
            Stand(camera != null ? camera.transform.position : (Vector3?)null);
        }

        /// <summary>Stands the lights for an eye at once. Public for tests and captures.</summary>
        public static void Direct(Vector3? eye)
        {
            Ensure();
            if (_instance != null)
            {
                _instance.Stand(eye);
            }
        }

        private void Stand(Vector3? eye)
        {
            Lamps.Clear();
            if (eye.HasValue && IsNight)
            {
                _eye = eye.Value;
                StreetLampSites.Near(_eye, ReachMetres, Lamps);
                Lamps.Sort(NearestFirst);
            }
            int count = Mathf.Min(Lamps.Count, MaxLights);
            while (_lights.Count < count)
            {
                _lights.Add(Make());
            }
            for (int i = 0; i < _lights.Count; i++)
            {
                Light light = _lights[i];
                if (i >= count)
                {
                    light.enabled = false;
                    continue;
                }
                float away = Vector3.Distance(Lamps[i], _eye);
                light.transform.position = Lamps[i];
                light.intensity = Brightness * Mathf.InverseLerp(ReachMetres, FullWithinMetres, away);
                light.enabled = light.intensity > 0.01f;
            }
            Lit = count;
        }

        private Light Make()
        {
            var host = new GameObject("StreetLamp") { hideFlags = HideFlags.HideAndDontSave };
            host.transform.SetParent(transform, false);
            host.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            Light light = host.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = LightRange;
            light.spotAngle = ConeDegrees;
            light.innerSpotAngle = InnerConeDegrees;
            light.color = Sodium;
            light.shadows = LightShadows.None;
            // It casts no shadows, so nothing but its mark keeps it off the rooms beside the road and out of the tunnel under it.
            CityLightLayers.MarkOutside(light);
            light.enabled = false;
            return light;
        }
    }
}
