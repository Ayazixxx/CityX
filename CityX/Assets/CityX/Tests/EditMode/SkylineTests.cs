using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The skyline tier past the streamed city (SkylineStreamer, CityMeshBuilder.BuildSkyline, 2026-10-15): what a group of chunks far from the
    /// viewer is made of -- a floor under the ground and a box inside every tall building -- and that the chunks round the viewer get nothing.
    /// </summary>
    public sealed class SkylineTests
    {
        private const int Seed = CitySeed.Default;
        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static readonly Vector2Int FarAway = new Vector2Int(1000000, 1000000);

        private static (MeshBuffer ground, MeshBuffer structures, Vector3 origin) Build(Vector2Int group, Vector2Int around, int exclude)
        {
            var ground = new MeshBuffer();
            var structures = new MeshBuffer();
            int n = SkylineStreamer.GroupChunks;
            var origin = new Vector3(group.x * SkylineStreamer.GroupSize, 0.0f, group.y * SkylineStreamer.GroupSize);
            CityMeshBuilder.BuildSkyline(World, group.x * n, group.y * n, n, origin, ground, structures, around, exclude);
            return (ground, structures, origin);
        }

        private static Vector2Int SpawnGroup()
        {
            World.Plan.TryFindSpawn(out double sx, out double sz);
            return SkylineStreamer.GroupOf(new Vector3((float)sx, 0.0f, (float)sz));
        }

        private static List<BuildingSite> TallBuildingsOf(Vector2Int group)
        {
            int n = SkylineStreamer.GroupChunks;
            var sites = new List<BuildingSite>();
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    foreach (int id in World.BuildingsOwnedBy(group.x * n + i, group.y * n + j))
                    {
                        BuildingSite site = World.Building(id);
                        if (CityMeshBuilder.InSkyline(site))
                        {
                            sites.Add(site);
                        }
                    }
                }
            }
            return sites;
        }

        [Test]
        public void AGroupFarFromTheViewerHasAFloorAndABoxPerTallBuilding()
        {
            Vector2Int g = SpawnGroup();
            (MeshBuffer ground, MeshBuffer structures, _) = Build(g, FarAway, SkylineStreamer.ExcludeRadiusChunks);
            int n = SkylineStreamer.GroupChunks;
            Assert.AreEqual(n * n * 4, ground.Vertices.Count, "a quad a chunk");
            List<BuildingSite> tall = TallBuildingsOf(g);
            Assert.Greater(tall.Count, 20, "a group at the spawn has tall buildings");
            Assert.GreaterOrEqual(structures.Vertices.Count, tall.Count * 20, "a box is five faces of four vertices");
            Assert.Less(structures.Vertices.Count, ChunkMeshData.MaxVerticesFor16BitIndices, "a group fits 16-bit indices");
            TestContext.WriteLine($"group {g}: {tall.Count} buildings, {structures.Vertices.Count} vertices, {structures.Triangles.Count / 3} triangles");
        }

        [Test]
        public void EveryBoxStandsInsideItsBuildingAndUnderItsRoof()
        {
            Vector2Int g = SpawnGroup();
            (_, MeshBuffer structures, Vector3 origin) = Build(g, FarAway, SkylineStreamer.ExcludeRadiusChunks);
            List<BuildingSite> tall = TallBuildingsOf(g);
            int outside = 0;
            foreach (Vector3 v in structures.Vertices)
            {
                Vector3 p = v + origin;
                bool inside = false;
                foreach (BuildingSite site in tall)
                {
                    // Into the building's frame: its axis is (AxisX, AxisZ), so u along it and w across it.
                    double dx = p.x - site.CentreX, dz = p.z - site.CentreZ;
                    double u = dx * site.AxisX + dz * site.AxisZ;
                    double w = -dx * site.AxisZ + dz * site.AxisX;
                    double hw = site.Params.Width * 0.5 + 1e-3, hd = (site.Sides >= 3 ? site.Params.Width : site.Params.Depth) * 0.5 + 1e-3;
                    // Under its roof, or within its top's reach (a tower's mast, spire or crown: World/TowerTop).
                    double ceiling = site.Top + TowerTop.For(site).Height + 1e-3;
                    if (System.Math.Abs(u) <= hw && System.Math.Abs(w) <= hd && p.y <= ceiling && p.y >= Mathf.Min(site.PodiumBase, site.BaseHeight) - CityMeshBuilder.SkylineFloorDrop - 0.5f - 1e-3)
                    {
                        inside = true;
                        break;
                    }
                }
                if (!inside)
                {
                    outside++;
                }
            }
            Assert.AreEqual(0, outside, "skyline vertices outside every tall building's footprint and height");
        }

        [Test]
        public void TheFloorLiesUnderTheGroundAtEveryCorner()
        {
            Vector2Int g = SpawnGroup();
            (MeshBuffer ground, _, Vector3 origin) = Build(g, FarAway, SkylineStreamer.ExcludeRadiusChunks);
            foreach (Vector3 v in ground.Vertices)
            {
                Vector3 p = v + origin;
                double land = World.Plan.GroundHeight(p.x, p.z);
                Assert.AreEqual(land - CityMeshBuilder.SkylineFloorDrop, p.y, 1e-3, $"the floor at ({p.x:F0}, {p.z:F0})");
            }
        }

        [Test]
        public void ChunksRoundTheViewerGetNoFloorAndNoBoxes()
        {
            Vector2Int g = SpawnGroup();
            int n = SkylineStreamer.GroupChunks;
            var around = new Vector2Int(g.x * n + n / 2, g.y * n + n / 2);   // the viewer in the middle of the group
            (MeshBuffer ground, MeshBuffer structures, _) = Build(g, around, SkylineStreamer.ExcludeRadiusChunks);
            Assert.AreEqual(0, ground.Vertices.Count, "the exclusion radius covers the whole group from its middle");
            Assert.AreEqual(0, structures.Vertices.Count);

            // The 5 x 5 chunks round a viewer well inside the group, on a chunk that owns a tall building (the group's middle need not: seed
            // 23481876's does not), so there is something for them to leave out.
            Vector2Int inside = around;
            foreach (BuildingSite site in TallBuildingsOf(g))
            {
                int lx = site.OwnerChunkX - g.x * n, ly = site.OwnerChunkY - g.y * n;
                if (lx >= 2 && lx <= n - 3 && ly >= 2 && ly <= n - 3)
                {
                    inside = new Vector2Int(site.OwnerChunkX, site.OwnerChunkY);
                    break;
                }
            }
            (MeshBuffer ground2, MeshBuffer structures2, _) = Build(g, inside, 2);
            Assert.AreEqual((n * n - 25) * 4, ground2.Vertices.Count, "the 5 x 5 chunks round the viewer have no floor, the rest do");
            (_, MeshBuffer all, _) = Build(g, FarAway, SkylineStreamer.ExcludeRadiusChunks);
            Assert.Greater(structures2.Vertices.Count, 0);
            Assert.Less(structures2.Vertices.Count, all.Vertices.Count, "the buildings owned by the chunks round the viewer are left out");
        }

        [Test]
        public void TheGroupMathsAgree()
        {
            Assert.AreEqual(new Vector2Int(0, 0), SkylineStreamer.GroupOf(new Vector3(10.0f, 0.0f, 10.0f)));
            Assert.AreEqual(new Vector2Int(-1, 2), SkylineStreamer.GroupOf(new Vector3(-10.0f, 0.0f, 2.5f * SkylineStreamer.GroupSize)));
            Assert.AreEqual(new Vector2Int(-1, 0), SkylineStreamer.ChunkOf(new Vector3(-1.0f, 0.0f, 1.0f)));
            Assert.AreEqual(84.0f, SkylineStreamer.Distance(new Vector2Int(1, 0), new Vector3(300.0f, 50.0f, 100.0f)), 1e-3f);
            Assert.AreEqual(0.0f, SkylineStreamer.Distance(new Vector2Int(0, 0), new Vector3(100.0f, 0.0f, 100.0f)), 1e-3f);
            // A group is near a viewer chunk when any of its chunks is within the exclusion radius plus the step.
            int reach = SkylineStreamer.ExcludeRadiusChunks + SkylineStreamer.RebuildStepChunks;
            Assert.IsTrue(SkylineStreamer.NearViewer(new Vector2Int(0, 0), new Vector2Int(0, 0)));
            Assert.IsTrue(SkylineStreamer.NearViewer(new Vector2Int(0, 0), new Vector2Int(SkylineStreamer.GroupChunks - 1 + reach, 0)));
            Assert.IsFalse(SkylineStreamer.NearViewer(new Vector2Int(0, 0), new Vector2Int(SkylineStreamer.GroupChunks + reach, 0)));
            Assert.IsFalse(SkylineStreamer.NearViewer(new Vector2Int(0, 0), new Vector2Int(-reach - 1, 3)));
        }

        /// <summary>
        /// The streamer measures every nearby group's distance every frame: it must make no garbage doing it (the three-argument Mathf.Max
        /// is the params overload and allocated an array per call, three quarters of all garbage while sprinting, docs/PERFORMANCE.md).
        /// </summary>
        [Test]
        public void MeasuringAGroupsDistanceMakesNoGarbage()
        {
            var p = new Vector3(123.4f, 5.0f, -678.9f);
            float sum = 0.0f;
            for (int i = 0; i < 10; i++) sum += SkylineStreamer.Distance(new Vector2Int(i, -i), p);   // warmed up (the JIT's own first-call work)
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                sum += SkylineStreamer.Distance(new Vector2Int(i % 20 - 10, i / 20 - 25), p);
                sum += SkylineStreamer.NearViewer(new Vector2Int(i % 7, i % 5), new Vector2Int(i % 11, i % 13)) ? 1.0f : 0.0f;
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, $"measuring distances allocated {allocated} bytes over 1000 groups (sum {sum})");
        }
    }
}
