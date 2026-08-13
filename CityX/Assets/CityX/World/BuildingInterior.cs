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
        /// A storey, in window rows. city_chunk.gd's STORY_HEIGHT_FLOORS.
        ///
        /// Measured in ROWS rather than metres so a floor always lands on the
        /// same grid the facade is divided by. A storey given in metres would
        /// put a slab through the middle of a window on most buildings.
        /// </summary>
        public const int StoreyHeightRows = 2;

        /// <summary>DOOR_HEIGHT_FLOORS. One row, for the same reason.</summary>
        public const int DoorHeightRows = 1;

        /// <summary>DOOR_WIDTH.</summary>
        public const float DoorWidth = 1.6f;

        /// <summary>
        /// Slab and wall thickness, city_chunk.gd's WALL_THICKNESS.
        ///
        /// A slab has real thickness rather than being one flat sheet: two
        /// coincident faces at the same y let the staircase from the floor
        /// above bleed through what should read as a solid ceiling, which was
        /// confirmed in a screenshot rather than argued about.
        /// </summary>
        public const float SlabThickness = 0.2f;

        /// <summary>The facade's own window-row height for a building this tall.</summary>
        public static float RowHeight(float buildingHeight)
        {
            return CellLayout.For(buildingHeight, CellLayout.NominalFloorMetres).Size;
        }

        public static float StoreyHeight(float buildingHeight)
        {
            return RowHeight(buildingHeight) * StoreyHeightRows;
        }

        public static float DoorHeight(float buildingHeight)
        {
            return RowHeight(buildingHeight) * DoorHeightRows;
        }

        /// <summary>How many floors a building this tall gets.</summary>
        public static int FloorCountFor(float buildingHeight)
        {
            return FloorCount(buildingHeight, StoreyHeight(buildingHeight));
        }

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
        /// The ceiling of one floor, above the building's own base.
        ///
        /// THE TOP FLOOR SWALLOWS EVERYTHING THE CAP LEFT OVER, which is what
        /// _build_interior does -- "size.y if is_top". On an ordinary building
        /// that is a few metres of extra headroom. On anything that hits
        /// MaxFloors it is every metre above the seventh storey, so the top
        /// floor of a supertall is a single room hundreds of metres tall with
        /// a staircase climbing all of it.
        ///
        /// IT COSTS MORE HERE THAN IT EVER DID IN GODOT, and not because the
        /// rule changed. city_chunk.gd caps a building at MAX_HEIGHT = 96m and
        /// _add_staircase's own comment budgets "up to ~65" steps for the
        /// worst top floor. This city's art direction goes to 320m, so the
        /// same rule now produces a 281m top floor and 1154 steps. The rule is
        /// carried across unchanged because it is a design call rather than a
        /// porting one, and CityMeshBuilderTests pins what it costs.
        /// </summary>
        public static float FloorTop(int floorIndex, int floorCount, float buildingHeight,
            float storeyHeight)
        {
            return floorIndex >= floorCount - 1
                ? buildingHeight
                : (floorIndex + 1) * storeyHeight;
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
            /// <summary>
            /// THE TOP OF THE FLIGHT, against the wall it climbs towards.
            ///
            /// Easy to read the other way round, and I did: _add_staircase's
            /// first step sits here and is the FULL rise tall, with each one
            /// after it shorter, out into the room. So the flight is highest
            /// at the wall. You walk from the middle of the room up to the
            /// wall and arrive at the floor above -- which is what
            /// _stairwell_for_floor's own "climbs toward the back wall" means,
            /// and it is correct.
            /// </summary>
            public readonly Vector3 WallPoint;

            /// <summary>
            /// The way the flight EXTENDS, which is away from the wall and
            /// therefore downward: from the top, at the wall, out to the foot
            /// in the middle of the room.
            /// </summary>
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

            /// <summary>
            /// The FOOT of the flight, out in the middle of the room -- where
            /// somebody starts climbing. See WallPoint for which end is which.
            /// </summary>
            public Vector3 FootOf(float run) => WallPoint + RunDirection * run;
        }

        /// <summary>One step: a box resting on the floor, as Godot builds them.</summary>
        public readonly struct Step
        {
            /// <summary>The middle of the box, including its own height.</summary>
            public readonly Vector3 Centre;

            /// <summary>
            /// The box's full size, in WORLD AXES.
            ///
            /// _add_staircase orients each step by the flight's own basis and
            /// sizes it (width, height, tread) in that basis. Every flight in
            /// this city runs along a world axis, so the same box is an
            /// axis-aligned one -- which is what a mesh buffer wants, and
            /// which cannot be silently misread as world XYZ the way a
            /// flight-local size can.
            /// </summary>
            public readonly Vector3 Size;

            public Step(Vector3 centre, Vector3 size)
            {
                Centre = centre;
                Size = size;
            }

            public float Top => Centre.y + Size.y * 0.5f;
        }

        /// <summary>
        /// Every step of one flight, in the building's own local space.
        ///
        /// EACH STEP IS A BOX STANDING ON THE FLOOR, not a floating tread:
        /// the one at the wall is the full rise tall and each one after it is
        /// shorter, so the stack reads as a solid flight from the side and
        /// there is nothing to fall through. That is _add_staircase's own
        /// shape, and it is why the flight is highest at the wall.
        /// </summary>
        public static Step[] Steps(Stairwell well, float run, float floorY, float rise)
        {
            int count = StepsFor(rise);
            float depth = run / count;
            var steps = new Step[count];

            // The flight's own two horizontal axes, as world extents. Both are
            // axis-aligned unit vectors at right angles, so adding them gives
            // exactly the box's X and Z size whichever way round the flight
            // happens to run.
            Vector3 across = Abs(well.WidthDirection) * StairWidth;
            Vector3 alongRun = Abs(well.RunDirection);

            for (int i = 0; i < count; i++)
            {
                float height = rise * (count - i) / count;
                Vector3 at = well.WallPoint + well.RunDirection * (depth * i + depth * 0.5f);
                steps[i] = new Step(
                    new Vector3(at.x, floorY + height * 0.5f, at.z),
                    across + alongRun * depth + new Vector3(0.0f, height, 0.0f));
            }
            return steps;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        /// <summary>
        /// The corner a basement's flight uses, city_chunk.gd's own
        /// BASEMENT_STAIR_CORNER. Fixed rather than rotating, because there is
        /// only ever one floor below ground and nothing above it to meet.
        /// </summary>
        public const int BasementStairCorner = 2;

        /// <summary>How much of the floor the carved corner room takes.</summary>
        public const float RoomWidthFraction = 0.4f;

        public const float RoomDepthFraction = 0.42f;

        /// <summary>
        /// Below this a room is a cupboard rather than a room, and the floor it
        /// was carved out of is left a sliver.
        /// </summary>
        public const float MinRoomSize = 2.4f;

        /// <summary>
        /// One floor's carved-off corner room, with the doorway into it.
        ///
        /// ALWAYS ON THE BACK WALL, never the wall the building's own entrance
        /// is in, and always the back corner this floor's staircase is not
        /// using. Every floor gets one, so more than one room reads as a
        /// property of the building rather than a rare extra.
        /// </summary>
        public readonly struct Room
        {
            /// <summary>False when the floor is too small to carve.</summary>
            public readonly bool Exists;

            /// <summary>Footprint in the building's own local X/Z.</summary>
            public readonly Rect Plan;

            /// <summary>
            /// The X of the wall carrying the doorway: the room's inner side,
            /// away from the exterior corner it hugs. Its other inner side,
            /// at Plan's maximum Z, is solid -- one door is enough.
            /// </summary>
            public readonly float DoorWallX;

            public readonly float DoorZ0;
            public readonly float DoorZ1;

            public Room(bool exists, Rect plan, float doorWallX, float doorZ0, float doorZ1)
            {
                Exists = exists;
                Plan = plan;
                DoorWallX = doorWallX;
                DoorZ0 = doorZ0;
                DoorZ1 = doorZ1;
            }
        }

        /// <summary>
        /// The corner this floor's room takes: 0 for the back-left, 1 for the
        /// back-right. Ported from _room_corner_for_floor.
        ///
        /// It dodges the staircase. Floors 0 and 1 put their flight in a BACK
        /// corner, so the room takes the other one. Floors 2 and 3 put theirs
        /// at the front, where the room cannot reach at all, so those just
        /// alternate to keep consecutive floors from looking identical.
        /// </summary>
        public static int RoomCornerFor(int floorIndex)
        {
            int stairCorner = ((floorIndex % 4) + 4) % 4;
            if (stairCorner == 0)
            {
                return 1;
            }
            if (stairCorner == 1)
            {
                return 0;
            }
            return ((floorIndex % 2) + 2) % 2 == 0 ? 0 : 1;
        }

        public static Room RoomFor(int floorIndex, float halfX, float halfZ)
        {
            if (halfX < MinRoomSize + 0.5f || halfZ < MinRoomSize + 0.5f)
            {
                return default;
            }

            // CAPPED AT HALF THE FOOTPRINT, less a margin. That is what makes
            // the room's clearance of the stairwell structural rather than
            // lucky: it cannot cross the building's own centre line, so it can
            // never reach a flight hugging the opposite corner either.
            float width = Mathf.Clamp(2.0f * halfX * RoomWidthFraction, MinRoomSize, halfX - 0.5f);
            float depth = Mathf.Clamp(2.0f * halfZ * RoomDepthFraction, MinRoomSize, halfZ - 0.5f);

            bool left = RoomCornerFor(floorIndex) == 0;
            float x0 = left ? -halfX : halfX - width;
            float x1 = left ? -halfX + width : halfX;
            float z0 = -halfZ;
            float z1 = -halfZ + depth;

            float doorZ0 = z0 + (depth - DoorWidth) * 0.5f;
            return new Room(true, Rect.MinMaxRect(x0, z0, x1, z1),
                left ? x1 : x0, doorZ0, doorZ0 + DoorWidth);
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
