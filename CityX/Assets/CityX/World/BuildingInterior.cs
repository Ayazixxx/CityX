using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Where a building's floor slabs and staircases go. Ported from
    /// city_chunk.gd's own _build_interior and _stairwell_for_floor.
    ///
    /// LAYOUT ONLY, NO GEOMETRY, which is the split this project already uses
    /// between CityChunkGenerator and CityMeshBuilder: the decisions live in a
    /// pure function that a test can interrogate, and the mesh builder turns
    /// the answer into triangles. Every unobvious number below was arrived at
    /// by something looking wrong in the Godot build, and is carried across as
    /// a reason rather than as a magic constant.
    /// </summary>
    public static class BuildingInterior
    {
        /// <summary>
        /// city_chunk.gd MAX_INTERIOR_FLOORS. A cap, not a target: past this
        /// the remainder is dumped onto the top floor as extra height, and a
        /// tall enough single flight stops being climbable at any sane
        /// floor angle.
        /// </summary>
        public const int MaxFloors = 7;

        public const float StairWidth = 1.8f;

        /// <summary>
        /// How far off the wall a flight starts, and -- subtracted from the
        /// room's half-extent -- how long its run is.
        /// </summary>
        public const float StairMargin = 1.0f;

        /// <summary>
        /// Flush against the side wall a flight hugs, but NOT zero: zero puts
        /// the stair's outer face exactly coplanar with the wall, and
        /// coincident geometry z-fights.
        /// </summary>
        public const float StairSideMargin = 0.05f;

        /// <summary>
        /// Rise per step. Steps per flight are derived from this and the
        /// flight's own rise rather than being a fixed count -- a fixed count
        /// at a tall story height came out over a metre per riser, giant
        /// against a 1.8m person.
        /// </summary>
        public const float StairStepHeight = 0.25f;

        /// <summary>
        /// How many floors fit in a building of this height, capped.
        /// </summary>
        public static int FloorCount(float height, float storyHeight)
        {
            if (storyHeight <= 0.0f)
            {
                return 1;
            }
            return Mathf.Clamp(Mathf.FloorToInt(height / storyHeight), 1, MaxFloors);
        }

        /// <summary>
        /// How far a flight runs into the room: all the way to the room's own
        /// centre, less the margin off the wall.
        ///
        /// REACHING THE CENTRE IS DELIBERATE. A short run, stopping a little
        /// way off the back wall, read as a stubby out-of-place little
        /// staircase rather than a real flight. The front half of the room
        /// past the stairs stays open floor.
        /// </summary>
        public static float RunFor(float halfX, float halfZ)
        {
            return Mathf.Max(Mathf.Min(halfX, halfZ) - StairMargin, 0.1f);
        }

        /// <summary>One floor's flight, and the hole it needs in the slab above.</summary>
        public readonly struct Stairwell
        {
            /// <summary>Where the flight starts, at the foot of the wall.</summary>
            public readonly Vector3 WallPoint;

            /// <summary>The way the flight climbs: away from the wall it starts at.</summary>
            public readonly Vector3 RunDirection;

            /// <summary>Across the flight, towards the side wall it hugs.</summary>
            public readonly Vector3 WidthDirection;

            /// <summary>
            /// The opening in the slab above, in the building's own local X/Z.
            /// It matches the stairs' own footprint EXACTLY. A wider hole was
            /// tried, to make the opening read as more than the stairs'
            /// silhouette, and it left strips of floor hanging over open air
            /// -- an unintentional pit rather than a stairwell.
            /// </summary>
            public readonly Rect Hole;

            public Stairwell(Vector3 wallPoint, Vector3 runDirection, Vector3 widthDirection,
                Rect hole)
            {
                WallPoint = wallPoint;
                RunDirection = runDirection;
                WidthDirection = widthDirection;
                Hole = hole;
            }

            /// <summary>Where the flight tops out, in local X/Z.</summary>
            public Vector3 TopOf(float run) => WallPoint + RunDirection * run;
        }

        /// <summary>
        /// Which corner this floor's flight hugs, and where that puts it.
        ///
        /// AGAINST A ROTATING CORNER, not the middle of the room and not the
        /// same wall every floor. Centred read as an odd freestanding block in
        /// an otherwise empty room. The same corner every floor made climbing
        /// several in a row feel like one room repeated. Rotating the corner
        /// keeps every flight flush against real walls while still climbing
        /// towards a different one each floor -- and, because the corner turns
        /// rather than jumping across, the next flight starts along the SAME
        /// wall the previous one topped out at. A walk along one wall to reach
        /// it, not a diagonal march across the open room.
        /// </summary>
        public static Stairwell StairwellFor(int floorIndex, float halfX, float halfZ, float run)
        {
            int corner = ((floorIndex % 4) + 4) % 4;
            float halfWidth = StairWidth * 0.5f;

            float sideX;
            float sideZ;
            bool nearAxisIsZ;
            switch (corner)
            {
                case 0: sideX = -1.0f; sideZ = -1.0f; nearAxisIsZ = true; break;   // back-left
                case 1: sideX = 1.0f; sideZ = -1.0f; nearAxisIsZ = false; break;   // back-right
                case 2: sideX = 1.0f; sideZ = 1.0f; nearAxisIsZ = true; break;     // front-right
                default: sideX = -1.0f; sideZ = 1.0f; nearAxisIsZ = false; break;  // front-left
            }

            // nearWall points from the room's centre at the wall this flight
            // climbs towards; hug points at the different side wall it stays
            // flush against the whole way. The run direction is the opposite of
            // nearWall by construction: away from that wall, into the room.
            Vector3 nearWall;
            Vector3 hug;
            float nearExtent;
            float hugExtent;
            if (nearAxisIsZ)
            {
                nearWall = new Vector3(0.0f, 0.0f, sideZ);
                hug = new Vector3(sideX, 0.0f, 0.0f);
                nearExtent = halfZ;
                hugExtent = halfX;
            }
            else
            {
                nearWall = new Vector3(sideX, 0.0f, 0.0f);
                hug = new Vector3(0.0f, 0.0f, sideZ);
                nearExtent = halfX;
                hugExtent = halfZ;
            }

            Vector3 runDirection = -nearWall;
            Vector3 wallPoint = nearWall * (nearExtent - StairMargin)
                                + hug * (hugExtent - StairSideMargin - halfWidth);
            Vector3 farPoint = wallPoint + runDirection * run;

            Vector3 a = wallPoint - hug * halfWidth;
            Vector3 b = farPoint + hug * halfWidth;
            var hole = Rect.MinMaxRect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z),
                Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));

            return new Stairwell(wallPoint, runDirection, hug, hole);
        }

        /// <summary>
        /// Steps in one flight. At least two, so a very short rise is still a
        /// pair of steps rather than a ramp or a single kerb.
        /// </summary>
        public static int StepsFor(float rise)
        {
            return Mathf.Max(2, Mathf.RoundToInt(rise / StairStepHeight));
        }
    }
}
