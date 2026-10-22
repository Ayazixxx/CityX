using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Which lit rooms (RoomLightGroup) get real lights, the pool those lights come from, and which of the lamps cast shadows. A lit room's glow
    /// (fittings, windows; ChunkRoomLights) is always shown; its lamps and bounces cost light in every pixel near them, so only the lit rooms within
    /// <see cref="LightReachMetres"/> of the camera get them, nearest first, up to <see cref="MaxRoomLights"/> in all (URP draws a limited number of
    /// lights at once, and one lit tower has hundreds). The <see cref="MaxShadowedLamps"/> lamps nearest the camera, within
    /// <see cref="ShadowReachMetres"/>, cast soft shadows (2026-10-09 research: shadows only where the player is, never everywhere): bodies, stairs
    /// and partitions throw shadows in the room you are in, and the wall between two rooms stops the light of one leaking into the other.
    ///
    /// THE LIGHTS ARE LENT, NOT OWNED (2026-10-14). A room has no Light of its own: when it gets its lights they come from a pool of spot lights
    /// kept here, are put in place, coloured and switched on, and they go back switched off when it goes dark, out of reach or off screen. Every
    /// powered room used to make its own when its chunk streamed in (up to 32 a room, 0.5-0.8 ms a room) only to leave them off. No more than
    /// <see cref="MaxRoomLights"/> are ever made, at most <see cref="MaxLightsMadePerFrame"/> a frame.
    ///
    /// Made on demand by the first chunk with powered rooms (hidden, kept across scenes). Every <see cref="Interval"/> seconds, from LateUpdate, and
    /// in the same frame when a switch is flipped or a chunk with lit rooms comes or goes.
    /// </summary>
    public sealed class RoomLightDirector : MonoBehaviour
    {
        /// <summary>Lit rooms further than this from the camera are their glow alone.</summary>
        public const float LightReachMetres = 110.0f;

        /// <summary>The most room lights (lamps and bounces) on at once, and so the most there ever are.</summary>
        public const int MaxRoomLights = 220;

        /// <summary>Lamps further than this from the camera never cast shadows.</summary>
        public const float ShadowReachMetres = 30.0f;

        /// <summary>The most lamps casting shadows at once (a spot's shadow is one extra render of what it sees).</summary>
        public const int MaxShadowedLamps = 4;

        /// <summary>How often the choice is made again, seconds.</summary>
        public const float Interval = 0.2f;

        /// <summary>
        /// The most lights the pool makes in one frame when it runs short (a player joining a session with many rooms lit around them): the rooms
        /// that did not get theirs get them over the next frames, nearest first. Making one costs 25-40 us (100 the first time in a session), so
        /// this is about a millisecond. Never fewer than the biggest room has (a lamp and a bounce per light point), or that room would wait for
        /// ever on an empty pool.
        /// </summary>
        public const int MaxLightsMadePerFrame = 2 * RoomLighting.MaxLightPoints;

        /// <summary>The most lit rooms whose bounced light (RoomFill) the facade shader draws at once: the nearest. MUST match CITYX_MAX_ROOM_FILLS.</summary>
        public const int MaxFilledRooms = 32;

        private static readonly Vector4[] Fills = new Vector4[MaxFilledRooms * RoomFill.Stride];
        private static readonly int FillsId = Shader.PropertyToID("_CityXRoomFills");
        private static readonly int FillCountId = Shader.PropertyToID("_CityXRoomFillCount");

        private static RoomLightDirector _instance;

        /// <summary>Every lit room whose chunk is on screen.</summary>
        private static readonly List<RoomLightGroup> LitRooms = new List<RoomLightGroup>();

        /// <summary>Chunks whose lit rooms changed since their glow was last drawn.</summary>
        private static readonly List<ChunkRoomLights> ToRedraw = new List<ChunkRoomLights>();

        private static readonly List<RoomLightGroup> Order = new List<RoomLightGroup>();
        private static readonly List<KeyValuePair<float, Light>> Shadowable = new List<KeyValuePair<float, Light>>();
        private static readonly System.Comparison<KeyValuePair<float, Light>> NearestFirst = (a, b) => a.Key.CompareTo(b.Key);
        private static readonly System.Comparison<RoomLightGroup> NearestRoomFirst =
            (a, b) => (a.Centre - _eye).sqrMagnitude.CompareTo((b.Centre - _eye).sqrMagnitude);
        private static Vector3 _eye;

        /// <summary>Something changed since the last pass: the next LateUpdate makes one, whatever the interval.</summary>
        private static bool _changed;

        private readonly Stack<Light> _free = new Stack<Light>();
        private int _made;
        private float _wait;

        /// <summary>How many room lights the pool has made so far (lent out or waiting). Never more than <see cref="MaxRoomLights"/>.</summary>
        public static int LightsMade => _instance != null ? _instance._made : 0;

        /// <summary>How many of them are lit now (lent to rooms).</summary>
        public static int LightsLent => _instance != null ? _instance._made - _instance._free.Count : 0;

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

        /// <summary>A room's switch went on (or its chunk came on screen with it on): it gets its lights at the next pass, this frame.</summary>
        internal static void Lit(RoomLightGroup room)
        {
            if (!LitRooms.Contains(room))
            {
                LitRooms.Add(room);
            }
            _changed = true;
            Ensure();
        }

        /// <summary>A room went dark (or its chunk went off screen): its lights go back to the pool now.</summary>
        internal static void Dark(RoomLightGroup room)
        {
            LitRooms.Remove(room);
            room.ReturnLights(_instance);
            _changed = true;
        }

        /// <summary>A chunk's lit rooms changed: its glow is drawn again before the frame is rendered.</summary>
        internal static void Redraw(ChunkRoomLights chunk)
        {
            ToRedraw.Add(chunk);
            _changed = true;
        }

        private void LateUpdate()
        {
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0.0f && !_changed)
            {
                return;
            }
            _wait = Interval;
            Camera camera = Camera.main;
            Pass(camera != null ? camera.transform.position : (Vector3?)null, MaxLightsMadePerFrame);
        }

        /// <summary>
        /// Decides, for every lit room, whether it has its lights and which lamps cast shadows, as seen from <paramref name="eye"/> (null: no camera;
        /// every lit room gets its lights up to the budget, and nothing casts a shadow), and draws the glow of chunks whose lit rooms changed. All
        /// at once, however many lights that makes. Public for tests and captures.
        /// </summary>
        public static void Direct(Vector3? eye)
        {
            Ensure();
            if (_instance != null)
            {
                _instance.Pass(eye, int.MaxValue);
            }
        }

        private void Pass(Vector3? eye, int mayMake)
        {
            _changed = false;
            for (int i = 0; i < ToRedraw.Count; i++)
            {
                if (ToRedraw[i] != null)
                {
                    ToRedraw[i].Redraw();
                }
            }
            ToRedraw.Clear();

            // A chunk thrown away without being switched off first (never, but cheap to be sure of) takes its rooms with it.
            for (int i = LitRooms.Count - 1; i >= 0; i--)
            {
                if (!LitRooms[i].IsShown)
                {
                    LitRooms[i].ReturnLights(this);
                    LitRooms.RemoveAt(i);
                }
            }
            Order.Clear();
            Order.AddRange(LitRooms);
            if (eye.HasValue)
            {
                _eye = eye.Value;
                Order.Sort(NearestRoomFirst);
            }

            // Which rooms get lights: the nearest in reach, while the budget lasts (a room too big for what is left is skipped, a smaller one
            // further off may still fit). Then the rooms that lose theirs give them back before the others take theirs from the pool.
            int budget = MaxRoomLights;
            float reach = LightReachMetres * LightReachMetres;
            foreach (RoomLightGroup room in Order)
            {
                bool near = !eye.HasValue || (room.Centre - eye.Value).sqrMagnitude <= reach;
                room.Wanted = near && room.LightCount <= budget;
                if (room.Wanted)
                {
                    budget -= room.LightCount;
                }
            }
            foreach (RoomLightGroup room in Order)
            {
                if (!room.Wanted)
                {
                    room.ReturnLights(this);
                }
            }
            foreach (RoomLightGroup room in Order)
            {
                if (!room.Wanted || room.LightsOn)
                {
                    continue;
                }
                int toMake = Mathf.Max(0, room.LightCount - _free.Count);
                if (toMake > mayMake)
                {
                    _changed = true;   // the pool is still growing: this room gets its lights in a later frame
                    continue;
                }
                mayMake -= toMake;
                room.TakeLights(this);
            }

            // The nearest few lamps in reach cast shadows; every other lamp of a lit room does not.
            Shadowable.Clear();
            foreach (RoomLightGroup room in Order)
            {
                if (!room.LightsOn)
                {
                    continue;
                }
                foreach (Light lamp in room.LampLights)
                {
                    if (lamp == null)
                    {
                        continue;
                    }
                    float distance = eye.HasValue ? Vector3.Distance(lamp.transform.position, eye.Value) : float.MaxValue;
                    Shadowable.Add(new KeyValuePair<float, Light>(distance, lamp));
                }
            }
            Shadowable.Sort(NearestFirst);
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

            // The bounced light of the nearest lit rooms, lamps or not (a lit room seen from down the street glows inside), as many as the
            // shader's array holds.
            int filled = 0;
            foreach (RoomLightGroup room in Order)
            {
                if (filled == MaxFilledRooms)
                {
                    break;
                }
                room.Fill.Pack(Fills, filled++);
            }
            Shader.SetGlobalVectorArray(FillsId, Fills);
            Shader.SetGlobalFloat(FillCountId, filled);
        }

        /// <summary>A spot light from the pool, made if the pool is empty. Its room puts it in place and switches it on.</summary>
        internal Light Take()
        {
            while (_free.Count > 0)
            {
                Light light = _free.Pop();
                if (light != null)
                {
                    return light;
                }
                _made--;   // destroyed behind the pool's back
            }
            // Under the director: hidden, kept across scenes, never under a chunk (a chunk's children are its own to destroy).
            var go = new GameObject("RoomLight") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            Light made = go.AddComponent<Light>();
            made.type = LightType.Spot;
            made.shadows = LightShadows.None;
            made.shadowStrength = 0.85f;
            CityLightLayers.MarkInside(made);   // it lights the room and whoever is in it, never the street (CityLightLayers)
            made.enabled = false;
            _made++;
            return made;
        }

        /// <summary>A light back into the pool, switched off and without a shadow.</summary>
        internal void Give(Light light)
        {
            if (light == null)
            {
                return;
            }
            light.enabled = false;
            light.shadows = LightShadows.None;
            _free.Push(light);
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }
            _instance = null;
            // Its lights go with it: no room may keep one, nor its bounced light.
            foreach (RoomLightGroup room in LitRooms)
            {
                room.ReturnLights(null);
            }
            Shader.SetGlobalFloat(FillCountId, 0.0f);
        }
    }
}
