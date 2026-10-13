namespace CityX.World
{
    /// <summary>
    /// Where another block of the same building stands against one of this one's walls (the leg of an L against the bar): a stretch of that wall,
    /// from the ground up to the other block's roof, that must have no windows, balconies or fins, because it is inside the other block.
    /// <see cref="U0"/>/<see cref="U1"/> are metres along the wall from its start (its minimum x for the north and south walls, its minimum z for east and
    /// west, in the building's own frame -- where window column 0 is); <see cref="Top"/> is metres above the floor.
    /// </summary>
    public readonly struct WallContact
    {
        public readonly WindowGrid.Wall Wall;
        public readonly float U0, U1, Top;

        public WallContact(WindowGrid.Wall wall, float u0, float u1, float top)
        {
            Wall = wall;
            U0 = u0;
            U1 = u1;
            Top = top;
        }
    }

    /// <summary>
    /// One building, resolved: where it stands, which way it faces, what it looks like and what it stands on.
    ///
    /// THE BUILDING IS A RECTANGLE IN ITS OWN FRAME, and the frame is turned to face its street. Everything that builds a
    /// building -- walls, windows, doors, stairs, rooms, roofs -- works in that unturned frame, exactly as it did when every
    /// building was lined up with the world axes; <see cref="MinX"/> .. <see cref="MaxZ"/> are that rectangle placed as if it were
    /// not turned, centred on the building. The one rotation is applied where the geometry is written out (MeshBuffer.SetFrame),
    /// and <see cref="LocalToWorld"/> / <see cref="WorldToLocal"/> convert for everything that has to find something in the world.
    ///
    /// Local +Z points out of the front, toward the street, and the door is on that wall; local +X is along the frontage.
    /// </summary>
    public readonly struct BuildingSite
    {
        /// <summary>Which building in the plan this is (stable for a seed). 0 is also a valid id; use <see cref="Exists"/>.</summary>
        public readonly int Id;

        public readonly BuildingParams Params;
        public readonly ZoneKind Zone;

        /// <summary>
        /// Ground floor height. The HIGHEST ground the footprint covers, so no corner is ever buried.
        /// </summary>
        public readonly float BaseHeight;

        /// <summary>The lowest ground the footprint covers. The gap up to BaseHeight is the podium that fills the downhill side.</summary>
        public readonly float PodiumBase;

        /// <summary>The ground just outside the door: how far the entrance has to climb.</summary>
        public readonly float DoorGroundHeight;

        /// <summary>World position of the centre of the footprint.</summary>
        public readonly double CentreX;
        public readonly double CentreZ;

        /// <summary>The world direction of local +X (unit). Local +Z is this turned a quarter counter-clockwise: (-AxisZ, AxisX).</summary>
        public readonly double AxisX;
        public readonly double AxisZ;

        /// <summary>The chunk that builds this building (the one its centre is in).</summary>
        public readonly int OwnerChunkX;
        public readonly int OwnerChunkY;

        /// <summary>A number every random choice about this building is derived from.</summary>
        public readonly int Seed;

        /// <summary>
        /// Where another volume of the same building stands on this one's roof, in this building's frame relative to its centre (x0, x1, z0, z1);
        /// all zero when nothing does. The stair that leads up to the roof must not come out under it.
        /// </summary>
        public readonly float ObstacleX0, ObstacleX1, ObstacleZ0, ObstacleZ1;

        /// <summary>
        /// This volume stands on the roof of another volume of the same building (a tower on a podium, a stage of a stepped tower): an attachment to the
        /// building, not a building of its own, so it has no shopfront, awnings or signs, and its door is not a street entrance.
        /// </summary>
        public readonly bool OnRoof;

        /// <summary>
        /// Sealed sculpture: built as its outside only, near or far -- no door, floors, rooms or stairs. The turned stages of a twisted tower, the blocks of
        /// a wonky pile, a cantilevered block: none has a doorway that would open onto anything. Its underside is drawn, because it can overhang.
        /// </summary>
        public readonly bool Shell;

        /// <summary>A second block at street level (the leg of an L, the arms of a U, the set-back half of a staggered pair): no shopfront of its own.</summary>
        public readonly bool Wing;

        /// <summary>The stretches of wall another block of the building stands against, or null. See <see cref="WallContact"/>.</summary>
        public readonly WallContact[] Contacts;

        /// <summary>A sealed block (<see cref="Shell"/>) stands on this one's roof: there is no stair up to the roof (BuildingStairs.Layout.RoofCovered).</summary>
        public readonly bool RoofSealed;

        /// <summary>
        /// 0 for a box; otherwise the building is round or many-sided: a regular polygon of this many sides (RoundFootprint) whose corners lie on the
        /// circle <see cref="BuildingParams.Width"/> across (Width and Depth are equal). One side faces the front, and the door is on it.
        /// </summary>
        public readonly int Sides;

        /// <summary>How far the front wall (where the door is) stands from the centre: half the depth of a box, the apothem of a polygon.</summary>
        public float FrontHalfDepth => Sides >= 3
            ? (float)(Params.Width * 0.5 * CosPiOver(Sides))
            : Params.Depth * 0.5f;

        /// <summary>cos(pi / sides), from literals for the side counts the city uses, so the plan (where a door's ground is sampled) needs no trigonometry.</summary>
        public static double CosPiOver(int sides)
        {
            switch (sides)
            {
                case 6: return 0.8660254037844387;
                case 8: return 0.9238795325112867;
                case 12: return 0.9659258262890683;
                case 16: return 0.9807852804032304;
                case 20: return 0.9876883405951378;
                default: return System.Math.Cos(System.Math.PI / sides);
            }
        }

        public bool HasRoofObstacle => ObstacleX1 > ObstacleX0 && ObstacleZ1 > ObstacleZ0;

        public static readonly BuildingSite None = default;

        public BuildingSite(int id, BuildingParams parameters, ZoneKind zone, float baseHeight, float podiumBase,
            float doorGroundHeight, double centreX, double centreZ, double axisX, double axisZ,
            int ownerChunkX, int ownerChunkY, int seed,
            float obstacleX0 = 0.0f, float obstacleX1 = 0.0f, float obstacleZ0 = 0.0f, float obstacleZ1 = 0.0f, bool onRoof = false,
            bool shell = false, bool wing = false, WallContact[] contacts = null, bool roofSealed = false, int sides = 0)
        {
            Sides = sides;
            RoofSealed = roofSealed;
            Shell = shell;
            Wing = wing;
            Contacts = contacts;
            Id = id;
            Params = parameters;
            Zone = zone;
            BaseHeight = baseHeight;
            PodiumBase = podiumBase;
            DoorGroundHeight = doorGroundHeight;
            CentreX = centreX;
            CentreZ = centreZ;
            AxisX = axisX;
            AxisZ = axisZ;
            OwnerChunkX = ownerChunkX;
            OwnerChunkY = ownerChunkY;
            Seed = seed;
            ObstacleX0 = obstacleX0;
            ObstacleX1 = obstacleX1;
            ObstacleZ0 = obstacleZ0;
            ObstacleZ1 = obstacleZ1;
            OnRoof = onRoof;
        }


        /// <summary>The same building with other wall contacts (PlanBuildings.AddPartyWalls adds the walls its neighbours stand against).</summary>
        public BuildingSite WithContacts(WallContact[] contacts)
        {
            return new BuildingSite(Id, Params, Zone, BaseHeight, PodiumBase, DoorGroundHeight, CentreX, CentreZ, AxisX, AxisZ, OwnerChunkX, OwnerChunkY,
                Seed, ObstacleX0, ObstacleX1, ObstacleZ0, ObstacleZ1, OnRoof, Shell, Wing, contacts, RoofSealed, Sides);
        }

        public bool Exists => Params.HasBuilding;

        /// <summary>An open car park (SpecialTypes.ParkingLot): a lot with no building on it, so nothing treats it as one (no interior, no "inside a building").</summary>
        public bool IsOpenGround => Params.SpecialType == SpecialTypes.ParkingLot;

        /// <summary>A multi-storey car park (SpecialTypes.ParkingGarage).</summary>
        public bool IsParkingGarage => Params.SpecialType == SpecialTypes.ParkingGarage;

        /// <summary>A shopping mall (SpecialTypes.Mall).</summary>
        public bool IsMall => Params.SpecialType == SpecialTypes.Mall;

        /// <summary>Height of the roof above sea level.</summary>
        public float Top => BaseHeight + Params.Height;

        /// <summary>How far the podium has to reach down on the low side.</summary>
        public float PodiumDepth => BaseHeight - PodiumBase;

        // The rectangle in the building's own frame, placed unturned around the centre (see the class comment).
        public double MinX => CentreX - Params.Width * 0.5;
        public double MaxX => CentreX + Params.Width * 0.5;
        public double MinZ => CentreZ - Params.Depth * 0.5;
        public double MaxZ => CentreZ + Params.Depth * 0.5;

        /// <summary>
        /// How high another block of the building stands against a stretch of one wall (metres above this floor; 0 when nothing does). The stretch is
        /// metres along the wall from its start (see <see cref="WallContact"/>).
        /// </summary>
        public float ContactTop(WindowGrid.Wall wall, float u0, float u1)
        {
            float top = 0.0f;
            if (Contacts == null)
            {
                return top;
            }
            for (int i = 0; i < Contacts.Length; i++)
            {
                WallContact c = Contacts[i];
                if (c.Wall == wall && c.U0 < u1 && c.U1 > u0 && c.Top > top)
                {
                    top = c.Top;
                }
            }
            return top;
        }

        /// <summary>
        /// The same as <see cref="ContactTop"/>, for a stretch given relative to the building's centre in its own frame (x for the north and south walls, z for
        /// east and west) -- the way balconies, fins and signs are placed.
        /// </summary>
        public float ContactTopCentred(WindowGrid.Wall wall, float c0, float c1)
        {
            float half = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South ? Params.Width * 0.5f : Params.Depth * 0.5f;
            return ContactTop(wall, c0 + half, c1 + half);
        }

        /// <summary>A point in the building's frame (relative to its centre) as a world position.</summary>
        public void LocalToWorld(double localX, double localZ, out double worldX, out double worldZ)
        {
            worldX = CentreX + localX * AxisX - localZ * AxisZ;
            worldZ = CentreZ + localX * AxisZ + localZ * AxisX;
        }

        /// <summary>A world position as a point in the building's frame, relative to its centre.</summary>
        public void WorldToLocal(double worldX, double worldZ, out double localX, out double localZ)
        {
            double dx = worldX - CentreX;
            double dz = worldZ - CentreZ;
            localX = dx * AxisX + dz * AxisZ;
            localZ = -dx * AxisZ + dz * AxisX;
        }

        /// <summary>
        /// A world position expressed in the unturned frame the builder uses (<see cref="MinX"/> .. <see cref="MaxZ"/>): the same
        /// point, but as if the building were lined up with the world axes about its centre.
        /// </summary>
        public void ToBuilderFrame(double worldX, double worldZ, out double frameX, out double frameZ)
        {
            WorldToLocal(worldX, worldZ, out double lx, out double lz);
            frameX = CentreX + lx;
            frameZ = CentreZ + lz;
        }
    }
}
