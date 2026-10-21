using System;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// One powered room's lights in a streamed chunk: where its lamps (spots shining down) and their bounces (spots shining up off the floor) go,
    /// their colour and brightness, and the glowing faces of its fittings and windows (CityMeshBuilder.RoomGlow), shown while its switch is on
    /// (World.RoomLights). Only buildings with power (World.PowerGrid) have any; a dead building's fittings are just part of the chunk's mesh, and
    /// no window anywhere is lit any other way.
    ///
    /// A PLAIN OBJECT, AND NOTHING IS MADE FOR IT UNTIL ITS SWITCH IS ON (2026-10-14). Every powered room used to be a GameObject with its glow
    /// mesh, its window mesh and its lamps and bounces as spot lights (up to 32), all made the moment its chunk streamed in: 0.5-0.8 ms a room,
    /// 10-16 ms of one frame for a powered block, and nearly all of it dark, since a room lights only when somebody flips its switch. Now its
    /// chunk (ChunkRoomLights) draws the glow and windows of its lit rooms, one mesh per light colour, and RoomLightDirector lends a lit room real
    /// lights from a pool while it is near enough to matter, and gives the nearest lamps shadows.
    /// </summary>
    public sealed class RoomLightGroup
    {
        private static readonly Quaternion ShiningDown = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        private static readonly Quaternion ShiningUp = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        /// <summary>The bounce is the room's light after a pale floor has had it: the same colour, a little greyed.</summary>
        private static readonly Color PaleFloor = new Color(0.85f, 0.85f, 0.85f);
        private const float FloorGreying = 0.3f;

        private readonly CityMeshBuilder.RoomGlow _glow;
        private readonly Vector3[] _lampPoints;
        private readonly Vector3[] _bouncePoints;
        private Light[] _lamps;
        private Light[] _bounces;

        internal RoomLightGroup(ChunkRoomLights chunk, CityMeshBuilder.RoomGlow glow)
        {
            Chunk = chunk;
            _glow = glow;
            Transform frame = chunk.transform;
            Vector3[] lamps = glow.Lights ?? Array.Empty<Vector3>();
            Vector3[] bounces = glow.Bounces ?? Array.Empty<Vector3>();
            _lampPoints = new Vector3[lamps.Length];
            _bouncePoints = new Vector3[bounces.Length];
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < lamps.Length; i++)
            {
                _lampPoints[i] = frame.TransformPoint(lamps[i]);
                sum += _lampPoints[i];
            }
            for (int i = 0; i < bounces.Length; i++)
            {
                _bouncePoints[i] = frame.TransformPoint(bounces[i]);
            }
            Centre = lamps.Length > 0 ? sum / lamps.Length : frame.position;
        }

        public RoomKey Key => _glow.Key;

        /// <summary>The room's light colour (by its kind of lighting, World.RoomLighting.ColourOf).</summary>
        public Color Colour => _glow.Colour;

        /// <summary>Its switch is on (and its chunk is on screen).</summary>
        public bool IsLit { get; internal set; }

        /// <summary>Whether it has its real lights now: it is lit, and RoomLightDirector has lent it lights (it is near enough, and within the budget).</summary>
        public bool LightsOn { get; private set; }

        /// <summary>The middle of its lamps, in the world.</summary>
        public Vector3 Centre { get; }

        /// <summary>How many real lights it has while they are on: a lamp and a bounce under it per cell of the room (RoomLighting.SplitFor).</summary>
        public int LightCount => _lampPoints.Length + _bouncePoints.Length;

        /// <summary>Its lamps while it has its lights (copies, for tests and tools); empty while it has none.</summary>
        public Light[] Lamps => LightsOn ? (Light[])_lamps.Clone() : Array.Empty<Light>();

        /// <summary>Its bounces while it has its lights (copies); empty while it has none.</summary>
        public Light[] Bounces => LightsOn ? (Light[])_bounces.Clone() : Array.Empty<Light>();

        /// <summary>The chunk it is in.</summary>
        internal ChunkRoomLights Chunk { get; }

        /// <summary>The faces it shows while lit (its fittings' glow and its windows, chunk-local).</summary>
        internal CityMeshBuilder.RoomGlow Glow => _glow;

        /// <summary>Its lamps while <see cref="LightsOn"/>, as they are (RoomLightDirector's, for shadows).</summary>
        internal Light[] LampLights => _lamps;

        /// <summary>Whether RoomLightDirector's pass this time wants it to have lights. Its own bookkeeping.</summary>
        internal bool Wanted;

        /// <summary>Its chunk is still there and on screen.</summary>
        internal bool IsShown => Chunk != null && Chunk.isActiveAndEnabled;

        /// <summary>Takes a light from <paramref name="director"/>'s pool for every lamp and bounce and puts it in place, lit, without a shadow.</summary>
        internal void TakeLights(RoomLightDirector director)
        {
            if (LightsOn)
            {
                return;
            }
            _lamps ??= new Light[_lampPoints.Length];
            _bounces ??= new Light[_bouncePoints.Length];
            for (int i = 0; i < _lamps.Length; i++)
            {
                _lamps[i] = Place(director.Take(), _lampPoints[i], ShiningDown, _glow.Colour, _glow.Range, _glow.Intensity,
                    RoomLighting.LampOuterAngle, RoomLighting.LampInnerAngle);
            }
            Color bounced = Color.Lerp(_glow.Colour, PaleFloor, FloorGreying);
            for (int i = 0; i < _bounces.Length; i++)
            {
                _bounces[i] = Place(director.Take(), _bouncePoints[i], ShiningUp, bounced, _glow.BounceRange, _glow.BounceIntensity,
                    RoomLighting.BounceOuterAngle, RoomLighting.BounceInnerAngle);
            }
            LightsOn = true;
        }

        /// <summary>Gives its lights back to <paramref name="director"/>'s pool (switched off, no shadow). Null: the pool is gone, and its lights with it.</summary>
        internal void ReturnLights(RoomLightDirector director)
        {
            if (!LightsOn)
            {
                return;
            }
            LightsOn = false;
            Return(_lamps, director);
            Return(_bounces, director);
        }

        private static void Return(Light[] lights, RoomLightDirector director)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (director != null)
                {
                    director.Give(lights[i]);
                }
                lights[i] = null;
            }
        }

        private static Light Place(Light light, Vector3 at, Quaternion shining, Color colour, float range, float intensity, float outerAngle,
            float innerAngle)
        {
            light.transform.SetPositionAndRotation(at, shining);
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.spotAngle = outerAngle;
            light.innerSpotAngle = innerAngle;
            light.shadows = LightShadows.None;
            light.enabled = true;
            return light;
        }
    }
}
