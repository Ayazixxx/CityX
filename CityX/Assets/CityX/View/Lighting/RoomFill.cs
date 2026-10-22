using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A lit room's bounced light, as the facade shader draws it (Shaders/FacadeForwardPass.hlsl, CityXRoomFill): an even light
    /// (World.RoomLightPlan.FillLight, in the room's colour) on every surface of the room -- floor, walls, ceiling, the underside of a stair, a
    /// fitting's housing -- that stops at its walls, its floor and its ceiling, and leaves out the rooms carved from it (a hall's corner room and
    /// stairwell, which have their own switch). It stands in for the light a real room bounces off its pale walls, which the lamps (spots shining
    /// down) cannot give: without it a lit room was dark right beside its fittings (owner, 2026-10-22).
    ///
    /// A SURFACE BELONGS TO THE ROOM IT FACES: the point <see cref="Nudge"/> in front of it is the one tested. So a floor belongs to the storey above
    /// it and a ceiling to the storey under it, never the other way round, and the two faces of a partition (built as one sheet) belong to the two
    /// rooms either side of it. A plain value: RoomLightDirector packs the nearest lit rooms' into the shader's array.
    /// </summary>
    public readonly struct RoomFill
    {
        /// <summary>How many Vector4s one room takes in the shader's array. MUST match CITYX_ROOM_FILL_STRIDE.</summary>
        public const int Stride = 6;

        /// <summary>How far in front of a surface its room is looked for, metres. MUST match CITYX_ROOM_FILL_NUDGE.</summary>
        public const float Nudge = 0.05f;

        /// <summary>A cut that takes nothing out: a rectangle inside out.</summary>
        private static readonly Vector4 NoCut = new Vector4(1e6f, 1e6f, -1e6f, -1e6f);

        /// <summary>The building's frame in the world: its origin at the room's floor, and its x and z axes (horizontal, unit; x and z of each).</summary>
        private readonly Vector3 _origin;
        private readonly Vector2 _axisX;
        private readonly Vector2 _axisZ;

        /// <summary>The floor plan it fills and the rooms cut from it, in the building's frame, as (xMin, zMin, xMax, zMax).</summary>
        private readonly Vector4 _area;
        private readonly Vector4 _cutA;
        private readonly Vector4 _cutB;

        /// <summary>From its floor to its ceiling, metres.</summary>
        private readonly float _height;

        /// <summary>Its light, linear: its colour times its brightness.</summary>
        public readonly Color Light;

        /// <summary>
        /// The bounced light of <paramref name="plan"/>'s room, in a building whose frame (origin and x and z axes) is given in the world, in
        /// <paramref name="colour"/> (an authoring colour, as a Light's is).
        /// </summary>
        public RoomFill(Vector3 frameOrigin, Vector3 frameX, Vector3 frameZ, RoomLightPlan plan, Color colour)
        {
            _origin = frameOrigin + Vector3.up * plan.FloorY;
            _axisX = new Vector2(frameX.x, frameX.z).normalized;
            _axisZ = new Vector2(frameZ.x, frameZ.z).normalized;
            _area = Corners(plan.FillArea);
            _cutA = plan.Cuts.Count > 0 ? Corners(plan.Cuts[0]) : NoCut;
            _cutB = plan.Cuts.Count > 1 ? Corners(plan.Cuts[1]) : NoCut;
            _height = plan.CeilingY - plan.FloorY;
            Light = colour.linear * plan.FillLight;
        }

        /// <summary>Whether the surface at <paramref name="point"/> facing <paramref name="normal"/> (the world) is lit by it. The shader's test, kept the same.</summary>
        public bool Covers(Vector3 point, Vector3 normal)
        {
            Vector3 q = point + normal * Nudge - _origin;
            var local = new Vector2(q.x * _axisX.x + q.z * _axisX.y, q.x * _axisZ.x + q.z * _axisZ.y);
            return q.y >= 0.0f && q.y <= _height && Inside(local, _area) && !Inside(local, _cutA) && !Inside(local, _cutB);
        }

        /// <summary>Writes it into the shader's array at room slot <paramref name="room"/> (<see cref="Stride"/> entries). MUST match CityXRoomFill.</summary>
        public void Pack(Vector4[] into, int room)
        {
            int at = room * Stride;
            into[at] = new Vector4(_origin.x, _origin.z, _origin.y, _height);
            into[at + 1] = new Vector4(_axisX.x, _axisX.y, _axisZ.x, _axisZ.y);
            into[at + 2] = _area;
            into[at + 3] = _cutA;
            into[at + 4] = _cutB;
            into[at + 5] = new Vector4(Light.r, Light.g, Light.b, 0.0f);
        }

        private static Vector4 Corners(Rect r) => new Vector4(r.xMin, r.yMin, r.xMax, r.yMax);

        private static bool Inside(Vector2 p, Vector4 r) => p.x >= r.x && p.y >= r.y && p.x <= r.z && p.y <= r.w;
    }
}
