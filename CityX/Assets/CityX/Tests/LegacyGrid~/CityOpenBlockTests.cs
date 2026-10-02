using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Open blocks -- parks and plazas. A downtown open block is a paved
    /// plaza; the nature treatment (a lawn with blocky trees in a park, a few
    /// trees in a plaza) is built but SWITCHED OFF -- "i didnt want any nature
    /// yet" -- behind CityMeshBuilder.DrawNature, so the tests that exercise
    /// it turn it on and put it back. An industrial open block stays bare yard.
    /// </summary>
    public class CityOpenBlockTests
    {
        private const int Seed = 20260810;

        [TearDown]
        public void NatureGoesBackToItsShippedDefault()
        {
            CityMeshBuilder.DrawNature = false;
        }

        private static IEnumerable<(int x, int y)> Windows()
        {
            foreach (var (wx, wy) in new[] { (0, 0), (140, 140), (-140, 140), (140, -140), (-280, -140), (280, 140), (0, 280), (-140, -280) })
            {
                for (int x = wx - 30; x <= wx + 30; x++)
                {
                    for (int y = wy - 30; y <= wy + 30; y++)
                    {
                        yield return (x, y);
                    }
                }
            }
        }

        private static void Build(CityWorld world, int x, int y,
            out MeshBuffer pavement, out MeshBuffer greenery, out MeshBuffer bark)
        {
            var bridges = new SkybridgeLayout(Seed);
            pavement = new MeshBuffer();
            greenery = new MeshBuffer();
            bark = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                new MeshBuffer(), pavement, new MeshBuffer(),
                greenery: greenery, bark: bark);
        }

        /// <summary>The first open chunk of a wanted kind of zone.</summary>
        private static (int x, int y) FindOpenChunk(CityWorld world, System.Func<ZoneKind, bool> wanted)
        {
            foreach (var (x, y) in Windows())
            {
                LotRole role = world.RoleAt(x, y);
                if (role.Kind == LotKind.Open && wanted(CityChunkGenerator.ZoneOfRole(role)))
                {
                    return (x, y);
                }
            }
            Assert.Fail("no open chunk of the wanted kind anywhere in the sample");
            return (0, 0);
        }

        [Test]
        public void AParkChunkGetsALawnAndTreesInsideItsOwnSquare()
        {
            CityMeshBuilder.DrawNature = true;
            var world = new CityWorld(Seed);
            var (x, y) = FindOpenChunk(world, z => !CityMeshBuilder.IsPlazaZone(z) && z != ZoneKind.Industrial);
            Build(world, x, y, out MeshBuffer pavement, out MeshBuffer greenery, out MeshBuffer bark);

            Assert.IsFalse(greenery.IsEmpty, "a park chunk grew no lawn or canopy");
            Assert.IsFalse(bark.IsEmpty, "a park chunk grew no trunks");

            float size = CityLayout.ChunkSize;
            float x0 = x * size, z0 = y * size;
            float pad = world.Terrain.PadHeightFor(x, y);
            foreach (MeshBuffer buffer in new[] { greenery, bark })
            {
                foreach (Vector3 v in buffer.Vertices)
                {
                    Assert.That(v.x, Is.InRange(x0 - 0.001f, x0 + size + 0.001f), "a tree or lawn leaves its chunk on X");
                    Assert.That(v.z, Is.InRange(z0 - 0.001f, z0 + size + 0.001f), "a tree or lawn leaves its chunk on Z");
                    Assert.GreaterOrEqual(v.y, pad, "greenery sits below the ground");
                    Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
                }
            }

            // The lawn is one flat quad the size of the chunk, floating just
            // above the pad.
            bool foundLawn = false;
            for (int i = 0; i + 3 < greenery.Vertices.Count; i++)
            {
                if (Mathf.Abs(greenery.Vertices[i].y - (pad + CityMeshBuilder.OpenSurfaceLift)) < 0.001f &&
                    greenery.Normals[i].y > 0.99f)
                {
                    foundLawn = true;
                    break;
                }
            }
            Assert.IsTrue(foundLawn, "no lawn quad at the pad");
            Assert.IsTrue(pavement.IsEmpty || !HasQuadAt(pavement, pad + CityMeshBuilder.OpenSurfaceLift),
                "a park was paved as well as planted");
        }

        /// <summary>
        /// EXPLICIT FEEDBACK: "remove the trees i didnt want any nature yet."
        /// Shipped default: no lawn, no trees anywhere, and a park is bare
        /// terrace exactly as before open blocks were drawn at all.
        /// </summary>
        [Test]
        public void NatureIsOffByDefaultSoParksStayBareTerrace()
        {
            Assert.IsFalse(CityMeshBuilder.DrawNature, "nature must ship switched off");

            var world = new CityWorld(Seed);
            var (x, y) = FindOpenChunk(world, z => !CityMeshBuilder.IsPlazaZone(z) && z != ZoneKind.Industrial);
            Build(world, x, y, out MeshBuffer pavement, out MeshBuffer greenery, out MeshBuffer bark);

            Assert.IsTrue(greenery.IsEmpty, "a park grew a lawn or canopy with nature off");
            Assert.IsTrue(bark.IsEmpty, "a park grew trees with nature off");
            float pad = world.Terrain.PadHeightFor(x, y);
            Assert.IsFalse(HasQuadAt(pavement, pad + CityMeshBuilder.OpenSurfaceLift), "a park was paved");
        }

        [Test]
        public void NoOpenChunkAnywhereGrowsATreeWhileNatureIsOff()
        {
            var world = new CityWorld(Seed);
            int open = 0;
            foreach (var (x, y) in Windows())
            {
                if (world.RoleAt(x, y).Kind != LotKind.Open)
                {
                    continue;
                }
                Build(world, x, y, out _, out MeshBuffer greenery, out MeshBuffer bark);
                open++;
                Assert.IsTrue(greenery.IsEmpty && bark.IsEmpty, $"open chunk ({x},{y}) grew greenery with nature off");
                if (open >= 200)
                {
                    break;
                }
            }
            Assert.Greater(open, 50, "too few open chunks sampled");
        }

        /// <summary>A plaza is paving, not nature: it stays with nature off, just without trees.</summary>
        [Test]
        public void ADowntownOpenBlockIsAPavedPlazaAndGrowsNoTreesWithNatureOff()
        {
            var world = new CityWorld(Seed);
            var (x, y) = FindOpenChunk(world, CityMeshBuilder.IsPlazaZone);
            Build(world, x, y, out MeshBuffer pavement, out MeshBuffer greenery, out MeshBuffer bark);

            float pad = world.Terrain.PadHeightFor(x, y);
            Assert.IsTrue(HasQuadAt(pavement, pad + CityMeshBuilder.OpenSurfaceLift), "a plaza has no paving");
            Assert.IsTrue(greenery.IsEmpty && bark.IsEmpty, "a plaza grew trees with nature off");
        }

        [Test]
        public void ADowntownOpenBlockIsAPavedPlazaWithAFewTreesWhenNatureIsOn()
        {
            CityMeshBuilder.DrawNature = true;
            var world = new CityWorld(Seed);
            var (x, y) = FindOpenChunk(world, CityMeshBuilder.IsPlazaZone);
            Build(world, x, y, out MeshBuffer pavement, out MeshBuffer greenery, out MeshBuffer bark);

            float pad = world.Terrain.PadHeightFor(x, y);
            Assert.IsTrue(HasQuadAt(pavement, pad + CityMeshBuilder.OpenSurfaceLift),
                "a plaza has no paving");
            Assert.IsFalse(HasQuadAt(greenery, pad + CityMeshBuilder.OpenSurfaceLift),
                "a plaza was given a lawn");

            int trunks = bark.Vertices.Count / 20;   // five quads a trunk
            Assert.That(trunks, Is.InRange(1, 3), "a plaza should have a handful of trees, not a park's worth");
        }

        [Test]
        public void AnIndustrialOpenBlockStaysBareYard()
        {
            var world = new CityWorld(Seed);
            var (x, y) = FindOpenChunk(world, z => z == ZoneKind.Industrial);
            Build(world, x, y, out MeshBuffer pavement, out MeshBuffer greenery, out MeshBuffer bark);
            Assert.IsTrue(greenery.IsEmpty && bark.IsEmpty, "an industrial yard grew a garden");
            float pad = world.Terrain.PadHeightFor(x, y);
            Assert.IsFalse(HasQuadAt(pavement, pad + CityMeshBuilder.OpenSurfaceLift), "an industrial yard was paved as a plaza");
        }

        /// <summary>Greenery belongs to open blocks alone.</summary>
        [Test]
        public void OnlyOpenChunksGrowGreenery()
        {
            var world = new CityWorld(Seed);
            int checkedCount = 0;
            for (int x = -25; x <= 25; x++)
            {
                for (int y = -25; y <= 25; y++)
                {
                    if (world.RoleAt(x, y).Kind == LotKind.Open)
                    {
                        continue;
                    }
                    Build(world, x, y, out _, out MeshBuffer greenery, out MeshBuffer bark);
                    checkedCount++;
                    Assert.IsTrue(greenery.IsEmpty && bark.IsEmpty, $"({x},{y}) is not open but grew greenery");
                }
            }
            Assert.Greater(checkedCount, 1000);
        }

        /// <summary>A block is decorated by its own chunks alone -- rebuilding gives exactly the same trees.</summary>
        [Test]
        public void TheSameChunkAlwaysGrowsTheSameTrees()
        {
            CityMeshBuilder.DrawNature = true;
            var world = new CityWorld(Seed);
            var (x, y) = FindOpenChunk(world, z => !CityMeshBuilder.IsPlazaZone(z) && z != ZoneKind.Industrial);
            Build(world, x, y, out _, out MeshBuffer greenery1, out MeshBuffer bark1);
            Build(new CityWorld(Seed), x, y, out _, out MeshBuffer greenery2, out MeshBuffer bark2);

            Assert.AreEqual(greenery1.Vertices.Count, greenery2.Vertices.Count);
            Assert.AreEqual(bark1.Vertices.Count, bark2.Vertices.Count);
            for (int i = 0; i < greenery1.Vertices.Count; i++)
            {
                Assert.AreEqual(greenery1.Vertices[i], greenery2.Vertices[i]);
            }
        }

        [Test]
        public void TreeCountsStayInsideTheirBounds()
        {
            CityMeshBuilder.DrawNature = true;
            var world = new CityWorld(Seed);
            int parks = 0;
            foreach (var (x, y) in Windows())
            {
                LotRole role = world.RoleAt(x, y);
                if (role.Kind != LotKind.Open ||
                    CityMeshBuilder.IsPlazaZone(CityChunkGenerator.ZoneOfRole(role)) ||
                    CityChunkGenerator.ZoneOfRole(role) == ZoneKind.Industrial)
                {
                    continue;
                }
                Build(world, x, y, out _, out _, out MeshBuffer bark);
                int trunks = bark.Vertices.Count / 20;
                Assert.That(trunks, Is.InRange(CityMeshBuilder.MinTreesPerChunk, CityMeshBuilder.MaxTreesPerChunk),
                    $"({x},{y}) has {trunks} trees");
                if (++parks >= 120)
                {
                    break;
                }
            }
            Assert.Greater(parks, 30, "too few park chunks sampled");
        }

        /// <summary>
        /// Two trees in one chunk never share the same spot: the grid the
        /// trunks are dropped on is 3x3 and every cell is used at most once.
        /// </summary>
        [Test]
        public void TreesInOneChunkNeverStandOnTheSameSpot()
        {
            CityMeshBuilder.DrawNature = true;
            var world = new CityWorld(Seed);
            var (x, y) = FindOpenChunk(world, z => !CityMeshBuilder.IsPlazaZone(z) && z != ZoneKind.Industrial);
            Build(world, x, y, out _, out _, out MeshBuffer bark);

            var centres = new List<Vector2>();
            for (int i = 0; i + 19 < bark.Vertices.Count; i += 20)
            {
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int j = i; j < i + 20; j++)
                {
                    minX = Mathf.Min(minX, bark.Vertices[j].x);
                    maxX = Mathf.Max(maxX, bark.Vertices[j].x);
                    minZ = Mathf.Min(minZ, bark.Vertices[j].z);
                    maxZ = Mathf.Max(maxZ, bark.Vertices[j].z);
                }
                centres.Add(new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f));
            }

            for (int a = 0; a < centres.Count; a++)
            {
                for (int b = a + 1; b < centres.Count; b++)
                {
                    Assert.Greater(Vector2.Distance(centres[a], centres[b]), 2.0f, "two trees overlap");
                }
            }
        }

        private static bool HasQuadAt(MeshBuffer buffer, float y)
        {
            for (int i = 0; i < buffer.Vertices.Count; i++)
            {
                if (Mathf.Abs(buffer.Vertices[i].y - y) < 0.001f && buffer.Normals[i].y > 0.99f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
