using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// The city, as a queryable function of the world seed.
    ///
    /// A thin face over <see cref="PlanWorld"/> -- the Subversion-style plan of roads, blocks, lots and buildings -- so that
    /// everything else (the chunk builder, glass, doors, the map, the weather) asks one object. The plan is generated once per seed
    /// and shared; everything in it is read-only, so any thread may ask in any order and get the same city.
    ///
    /// A "chunk" is still a 24 m square of the world, but only as a unit of STREAMING and of the map: it no longer decides what
    /// stands there. Roads wind through chunks at any angle and a building belongs to whichever chunk its centre is in.
    /// </summary>
    public sealed class CityWorld
    {
        /// <summary>Salt for turning a chunk coordinate into that chunk's own seed.</summary>
        private const ulong ChunkSeedSalt = 0x43484E4BUL;   // "CHNK"

        private readonly ulong _seed;

        public int Seed { get; }
        public PlanWorld Plan { get; }
        public TerrainField Terrain { get; }

        public CityWorld(int worldSeed)
        {
            Seed = worldSeed;
            _seed = unchecked((ulong)(long)worldSeed);
            Plan = PlanWorld.For(worldSeed);
            Terrain = new TerrainField(Plan);
        }

        /// <summary>A seed a chunk can roll its own small decisions from (props, spawns). Defined once, here.</summary>
        public int ChunkSeed(int chunkX, int chunkY)
        {
            return unchecked((int)WorldHash.Coord(chunkX, chunkY, ChunkSeedSalt, _seed));
        }

        public int BuildingCount => Plan.Buildings.Length;

        public BuildingSite Building(int id) => Plan.Buildings[id];

        /// <summary>The buildings whose centres are in this chunk -- the ones it builds.</summary>
        public List<int> BuildingsOwnedBy(int chunkX, int chunkY) => Plan.BuildingsOwnedBy(chunkX, chunkY);

        /// <summary>The building a world position is inside (or within <paramref name="margin"/> metres of the walls).</summary>
        public bool TryBuildingAt(double x, double z, out BuildingSite site, double margin = 0.0)
        {
            int id = Plan.BuildingAt(x, z, margin);
            if (id < 0)
            {
                site = BuildingSite.None;
                return false;
            }
            site = Plan.Buildings[id];
            return true;
        }
    }
}
