using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The tests' city round its spawn, for the tests of what is built along its streets (docs/AMERICAN_STREETS.md: the sidewalks, the
    /// street lights, what stands at the kerb): its chunks, its roads, and a chunk's ground as the game builds it.
    /// </summary>
    internal static class SpawnStreets
    {
        public const int Seed = CitySeed.Default;

        private static CityWorld _world;

        public static CityWorld World => _world ??= new CityWorld(Seed);

        public static PlanWorld Plan => World.Plan;

        /// <summary>One chunk's ground, built: every buffer the builder fills, and where the chunk's own coordinates start.</summary>
        public sealed class Surface
        {
            public readonly MeshBuffer Ground = new MeshBuffer(), Pavement = new MeshBuffer(), Structures = new MeshBuffer(), Yellow = new MeshBuffer(),
                White = new MeshBuffer(), Rail = new MeshBuffer(), Lit = new MeshBuffer();

            public Vector3 Origin;

            public Vec2 InTheWorld(Vector3 local) => new Vec2(local.x + Origin.x, local.z + Origin.z);
        }

        /// <summary>The chunks within <paramref name="reach"/> of the spawn's.</summary>
        public static IEnumerable<(int x, int y)> Chunks(int reach)
        {
            Plan.TryFindSpawn(out double sx, out double sz);
            int cx = PlanWorld.ChunkOf(sx), cy = PlanWorld.ChunkOf(sz);
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    yield return (cx + dx, cy + dy);
                }
            }
        }

        /// <summary>Every road that reaches into those chunks, once each.</summary>
        public static List<EdgeGeometry> Roads(int reach)
        {
            var seen = new HashSet<int>();
            var roads = new List<EdgeGeometry>();
            foreach ((int x, int y) in Chunks(reach))
            {
                foreach (int id in Plan.EdgesIn(x, y))
                {
                    EdgeGeometry g = Plan.Geometry.Edges[id];
                    if (g != null && seen.Add(id))
                    {
                        roads.Add(g);
                    }
                }
            }
            return roads;
        }

        /// <summary>A chunk's ground as the game builds it near (not the far version).</summary>
        public static Surface Build(int chunkX, int chunkY)
        {
            var surface = new Surface { Origin = new Vector3(chunkX * (float)PlanWorld.ChunkSize, 0.0f, chunkY * (float)PlanWorld.ChunkSize) };
            CitySurfaceBuilder.Build(World, chunkX, chunkY, surface.Origin, surface.Ground, surface.Pavement, surface.Structures, surface.Yellow, surface.White,
                surface.Rail, false, surface.Lit);
            return surface;
        }

        /// <summary>The chunk that holds a point of the world, built.</summary>
        public static Surface BuildAt(Vec2 at) => Build(PlanWorld.ChunkOf(at.X), PlanWorld.ChunkOf(at.Y));

        /// <summary>The pavement's height at a point of the world.</summary>
        public static float PavementHeight(Vec2 at) => (float)Plan.GroundHeight(at.X, at.Y) + CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight;
    }
}
