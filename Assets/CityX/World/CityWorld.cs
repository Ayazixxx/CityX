namespace CityX.World
{
    /// <summary>
    /// One building, resolved: where it is anchored, what it looks like, and
    /// what it stands on.
    /// </summary>
    public readonly struct BuildingSite
    {
        public readonly int AnchorX;
        public readonly int AnchorY;
        public readonly LotRole Role;
        public readonly BuildingParams Params;

        /// <summary>
        /// Ground floor height. The HIGHEST pad the footprint covers, so no
        /// corner is ever buried. See TerrainField.PadRangeForSpan.
        /// </summary>
        public readonly float BaseHeight;

        /// <summary>
        /// The lowest pad the footprint covers. The gap up to BaseHeight is the
        /// podium that fills the downhill side.
        /// </summary>
        public readonly float PodiumBase;

        public static readonly BuildingSite None = default;

        public BuildingSite(int anchorX, int anchorY, LotRole role, BuildingParams parameters,
            float baseHeight, float podiumBase)
        {
            AnchorX = anchorX;
            AnchorY = anchorY;
            Role = role;
            Params = parameters;
            BaseHeight = baseHeight;
            PodiumBase = podiumBase;
        }

        public bool Exists => Params.HasBuilding;

        /// <summary>Height of the roof above sea level.</summary>
        public float Top => BaseHeight + Params.Height;

        /// <summary>How far the podium has to reach down on the low side.</summary>
        public float PodiumDepth => BaseHeight - PodiumBase;

        public double CentreX => CityLayout.SpanCentre(AnchorX, Role.SpanX) + Params.OffsetX;
        public double CentreZ => CityLayout.SpanCentre(AnchorY, Role.SpanY) + Params.OffsetZ;

        public double MinX => CentreX - Params.Width * 0.5;
        public double MaxX => CentreX + Params.Width * 0.5;
        public double MinZ => CentreZ - Params.Depth * 0.5;
        public double MaxZ => CentreZ + Params.Depth * 0.5;
    }

    /// <summary>
    /// The city, as a queryable function of coordinate.
    ///
    /// WHY A FACADE RATHER THAN LETTING CALLERS WIRE IT UP. Answering "what
    /// building is at this chunk" needs the block massing, the generator and
    /// the terrain, in that order, plus the chunk-seed derivation that ties a
    /// coordinate to the world seed. Every caller doing that by hand is four
    /// chances to derive a seed slightly differently, and a seed derived
    /// slightly differently on the server than on the client is a different
    /// city with no error message.
    ///
    /// Everything here is a pure function of the world seed and a coordinate.
    /// No caching, no ordering, no state -- ask in any order from any process
    /// and get the same world.
    /// </summary>
    public sealed class CityWorld
    {
        /// <summary>Salt for turning a chunk coordinate into that chunk's own seed.</summary>
        private const ulong ChunkSeedSalt = 0x43484E4BUL;   // "CHNK"

        private readonly ulong _seed;

        public TerrainField Terrain { get; }
        public CityBlocks Blocks { get; }

        public CityWorld(int worldSeed)
        {
            _seed = unchecked((ulong)(long)worldSeed);
            Terrain = new TerrainField(worldSeed);
            Blocks = new CityBlocks(worldSeed);
        }

        /// <summary>
        /// The seed a chunk rolls its building from.
        ///
        /// Defined ONCE, here. The obvious alternative -- xor of two big primes
        /// times the coordinates, which is what the test file used to do -- has
        /// a nasty property: xor-based mixes collide easily on structured
        /// input, and a city grid is nothing but structured input. Reusing the
        /// avalanche hash costs nothing and does not have that failure.
        /// </summary>
        public int ChunkSeed(int chunkX, int chunkY)
        {
            return unchecked((int)WorldHash.Coord(chunkX, chunkY, ChunkSeedSalt, _seed));
        }

        public LotRole RoleAt(int chunkX, int chunkY)
        {
            return Blocks.RoleFor(chunkX, chunkY);
        }

        /// <summary>
        /// The building standing on this chunk, following a covered chunk back
        /// to the anchor that owns it. Returns a site with Exists == false for
        /// streets and for lots left as plazas.
        /// </summary>
        public BuildingSite BuildingAt(int chunkX, int chunkY)
        {
            LotRole role = RoleAt(chunkX, chunkY);
            if (role.Kind == LotKind.Street)
            {
                return BuildingSite.None;
            }

            int anchorX = role.AnchorX;
            int anchorY = role.AnchorY;
            if (role.Kind == LotKind.Consumed)
            {
                // The anchor's own role carries the authoritative span.
                role = RoleAt(anchorX, anchorY);
            }

            BuildingParams parameters = CityChunkGenerator.ParamsFor(ChunkSeed(anchorX, anchorY), role);
            if (!parameters.HasBuilding)
            {
                return BuildingSite.None;
            }

            Terrain.PadRangeForSpan(anchorX, anchorY, role.SpanX, role.SpanY,
                out float highest, out float lowest);

            return new BuildingSite(anchorX, anchorY, role, parameters, highest, lowest);
        }
    }
}
