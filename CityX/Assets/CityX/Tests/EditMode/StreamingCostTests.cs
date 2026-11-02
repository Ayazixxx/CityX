using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What made the city pop in round the player (owner, 2026-10-12: "I keep witnessing buildings loading in in real time up close"): a big
    /// building's stairs took up to 27 seconds to plan -- the walking check's grid coarsened on a big floor until no doorway was passable, so the
    /// planner tried every placement -- and the chunk that built it sat empty meanwhile, holding one of the streamer's few workers. These keep it
    /// from coming back. (StreamingPopInBench and ChunkCostBench measure the whole thing.)
    /// </summary>
    public sealed class StreamingCostTests
    {
        [Test]
        public void ADoorwayIsPassableOnABigFloor()
        {
            // A 120 x 40 m storey cut in two by a partition with a 1.2 m doorway: a body (0.45 m radius) gets through it.
            const float hx = 60.0f, hz = 20.0f;
            var solids = new List<Rect>
            {
                Rect.MinMaxRect(-hx, -0.1f, -0.6f, 0.1f),
                Rect.MinMaxRect(0.6f, -0.1f, hx, 0.1f),
            };
            List<Vector2> path = BuildingStairs.WalkPath(solids, hx, hz, new Vector2(0.0f, -10.0f), new Vector2(0.0f, 10.0f));
            Assert.IsNotNull(path, "the doorway reads as shut on a big floor");
            // And with the doorway closed there is no way across.
            var shut = new List<Rect> { Rect.MinMaxRect(-hx, -0.1f, hx, 0.1f) };
            Assert.IsNull(BuildingStairs.WalkPath(shut, hx, hz, new Vector2(0.0f, -10.0f), new Vector2(0.0f, 10.0f)), "a path through a solid wall");
        }

        [Test]
        public void AWalkAroundAPartitionStaysOnTheFloor()
        {
            // The way round the end of a partition: every waypoint on the floor, none inside the partition grown by the body.
            const float hx = 12.0f, hz = 8.0f;
            var solids = new List<Rect> { Rect.MinMaxRect(-12.0f, -0.1f, 6.0f, 0.1f) };
            List<Vector2> path = BuildingStairs.WalkPath(solids, hx, hz, new Vector2(-8.0f, -4.0f), new Vector2(-8.0f, 4.0f));
            Assert.IsNotNull(path);
            foreach (Vector2 p in path)
            {
                Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(hx));
                Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(hz));
                Assert.IsFalse(p.x < 6.0f + 0.45f && Mathf.Abs(p.y) < 0.1f + 0.45f, $"waypoint {p} is in the partition");
            }
        }

        [Test]
        public void AHugeBuildingPlansItsStairsInWellUnderASecond()
        {
            // The kind of building that took 27 s: 120 x 39 m and 171 m tall. A fresh seed each run so a cached plan cannot hide the cost.
            int seed = 7 + (int)(System.DateTime.Now.Ticks % 100000);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            BuildingStairs.Layout layout = BuildingStairs.PlanFor(120.0f, 39.0f, 171.0f, false, BuildingMass.Tower, ZoneKind.Core, seed, Rect.zero);
            double ms = clock.Elapsed.TotalMilliseconds;
            Assert.Less(ms, 1000.0, $"planning the stairs took {ms:0} ms");
            Assert.Greater(layout.FloorCount, 0);
            // Asked again, the plan is remembered.
            clock.Restart();
            BuildingStairs.Layout again = BuildingStairs.PlanFor(120.0f, 39.0f, 171.0f, false, BuildingMass.Tower, ZoneKind.Core, seed, Rect.zero);
            Assert.AreSame(layout, again, "the same building planned twice");
            Assert.Less(clock.Elapsed.TotalMilliseconds, 5.0);
        }

        [Test]
        public void NoChunkRoundTheSpawnTakesASecondToBuildInTheGamesOwnCity()
        {
            CityScale before = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own districts, where the big buildings are
            try
            {
                var world = new CityWorld(95998845);
                world.Plan.TryFindSpawn(out double sx, out double sz);
                int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);
                double worst = 0.0;
                Vector2Int worstChunk = default;
                for (int dy = -6; dy <= 6; dy++)
                {
                    for (int dx = -6; dx <= 6; dx++)
                    {
                        var buffers = new MeshBuffer[5];
                        for (int i = 0; i < 5; i++) buffers[i] = new MeshBuffer();
                        int cx = cx0 + dx, cy = cy0 + dy;
                        var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        CityMeshBuilder.BuildChunk(world, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[4], buffers[3],
                            new MeshBuffer(), null, new List<CityMeshBuilder.CrackedOpening>(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer());
                        double ms = clock.Elapsed.TotalMilliseconds;
                        if (ms > worst)
                        {
                            worst = ms;
                            worstChunk = new Vector2Int(cx, cy);
                        }
                    }
                }
                Assert.Less(worst, 1000.0, $"chunk {worstChunk} took {worst:0} ms to build in full detail");
            }
            finally
            {
                CityScale.Active = before;
            }
        }
    }
}
