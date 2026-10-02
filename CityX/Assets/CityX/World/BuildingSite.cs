namespace CityX.World
{
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

        public bool HasRoofObstacle => ObstacleX1 > ObstacleX0 && ObstacleZ1 > ObstacleZ0;

        public static readonly BuildingSite None = default;

        public BuildingSite(int id, BuildingParams parameters, ZoneKind zone, float baseHeight, float podiumBase,
            float doorGroundHeight, double centreX, double centreZ, double axisX, double axisZ,
            int ownerChunkX, int ownerChunkY, int seed,
            float obstacleX0 = 0.0f, float obstacleX1 = 0.0f, float obstacleZ0 = 0.0f, float obstacleZ1 = 0.0f)
        {
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
        }


        public bool Exists => Params.HasBuilding;

        /// <summary>Height of the roof above sea level.</summary>
        public float Top => BaseHeight + Params.Height;

        /// <summary>How far the podium has to reach down on the low side.</summary>
        public float PodiumDepth => BaseHeight - PodiumBase;

        // The rectangle in the building's own frame, placed unturned around the centre (see the class comment).
        public double MinX => CentreX - Params.Width * 0.5;
        public double MaxX => CentreX + Params.Width * 0.5;
        public double MinZ => CentreZ - Params.Depth * 0.5;
        public double MaxZ => CentreZ + Params.Depth * 0.5;

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
