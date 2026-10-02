using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The far version of a chunk (CityMeshBuilder.BuildChunk's far mode): a building that is its outside only.
    /// It has to be much lighter than the full chunk, keep the same silhouette, stay a well-formed solid, and
    /// still show glass.
    /// </summary>
    public sealed class FarChunkGeometryTests
    {
        private const int Seed = 61472093;

        private sealed class Built
        {
            public readonly MeshBuffer Ground = new MeshBuffer();
            public readonly MeshBuffer Concrete = new MeshBuffer();
            public readonly MeshBuffer Walls = new MeshBuffer();
            public readonly MeshBuffer Lit = new MeshBuffer();
            public readonly MeshBuffer Dark = new MeshBuffer();

            public MeshBuffer[] All => new[] { Ground, Concrete, Walls, Lit, Dark };

            public int Triangles()
            {
                int total = 0;
                foreach (MeshBuffer buffer in All) total += buffer.Triangles.Count / 3;
                return total;
            }

            public void Bounds(out Vector3 min, out Vector3 max)
            {
                min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (MeshBuffer buffer in All)
                {
                    foreach (Vector3 v in buffer.Vertices)
                    {
                        min = Vector3.Min(min, v);
                        max = Vector3.Max(max, v);
                    }
                }
            }
        }

        private static Built Build(CityWorld world, SkybridgeLayout bridges, int x, int y, bool far, GlassDamage damage = null)
        {
            var built = new Built();
            CityMeshBuilder.BuildChunk(world, bridges, x, y, Vector3.zero,
                built.Ground, built.Concrete, built.Walls, built.Lit, built.Dark,
                null, damage, null, null, new MeshBuffer(), null, null, null, null,
                includeSurface: true, far: far);
            return built;
        }

        private static bool IsBuildingChunk(BuildingSite site, int x, int y) =>
            site.Exists && site.AnchorX == x && site.AnchorY == y;

        [Test]
        public void TheFarVersionHasAFractionOfTheTriangles()
        {
            var world = new CityWorld(Seed);
            var bridges = new SkybridgeLayout(Seed);
            long full = 0;
            long far = 0;
            int buildings = 0;
            for (int x = -10; x <= 10; x++)
            {
                for (int y = -10; y <= 10; y++)
                {
                    if (!IsBuildingChunk(world.BuildingAt(x, y), x, y)) continue;
                    int f = Build(world, bridges, x, y, false).Triangles();
                    int n = Build(world, bridges, x, y, true).Triangles();
                    Assert.LessOrEqual(n, f, $"({x},{y}): the far version has MORE triangles than the full one");
                    full += f;
                    far += n;
                    buildings++;
                }
            }

            Assert.Greater(buildings, 50);
            Assert.Less(far, full * 0.3, $"far {far} triangles vs full {full}: the far version is not light enough to matter");
        }

        [Test]
        public void TheFarVersionKeepsTheSilhouette()
        {
            var world = new CityWorld(Seed);
            var bridges = new SkybridgeLayout(Seed);
            int checkedBuildings = 0;
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    if (!IsBuildingChunk(world.BuildingAt(x, y), x, y)) continue;
                    Built full = Build(world, bridges, x, y, false);
                    Built far = Build(world, bridges, x, y, true);
                    full.Bounds(out Vector3 fullMin, out Vector3 fullMax);
                    far.Bounds(out Vector3 farMin, out Vector3 farMax);

                    Assert.AreEqual(fullMax.y, farMax.y, 0.05f, $"({x},{y}): the roof is at a different height");
                    Assert.GreaterOrEqual(farMin.x, fullMin.x - 0.12f, $"({x},{y}) far sticks out west");
                    Assert.GreaterOrEqual(farMin.z, fullMin.z - 0.12f, $"({x},{y}) far sticks out south");
                    Assert.LessOrEqual(farMax.x, fullMax.x + 0.12f, $"({x},{y}) far sticks out east");
                    Assert.LessOrEqual(farMax.z, fullMax.z + 0.12f, $"({x},{y}) far sticks out north");
                    checkedBuildings++;
                }
            }
            Assert.Greater(checkedBuildings, 30);
        }

        [Test]
        public void EveryFarFaceIsFiniteAndWoundTheWayItsNormalClaims()
        {
            var world = new CityWorld(Seed);
            var bridges = new SkybridgeLayout(Seed);
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    Built far = Build(world, bridges, x, y, true);
                    foreach (MeshBuffer buffer in far.All)
                    {
                        foreach (Vector3 v in buffer.Vertices)
                        {
                            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                                || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z), $"({x},{y}) has a non-finite vertex");
                        }
                        for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
                        {
                            Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                            Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                            Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                            Vector3 geometric = Vector3.Cross(b - a, c - a);
                            if (geometric.sqrMagnitude < 1e-10f) continue;   // a sliver: no orientation to check
                            Vector3 claimed = buffer.Normals[buffer.Triangles[t]];
                            Assert.Greater(Vector3.Dot(geometric.normalized, claimed), 0.5f,
                                $"({x},{y}) triangle {t / 3} is wound against its normal");
                        }
                    }
                }
            }
        }

        [Test]
        public void ATallFarBuildingStillShowsItsGlass()
        {
            var world = new CityWorld(Seed);
            var bridges = new SkybridgeLayout(Seed);
            int checkedBuildings = 0;
            for (int x = -10; x <= 10; x++)
            {
                for (int y = -10; y <= 10; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!IsBuildingChunk(site, x, y) || site.Params.Height < 60.0f) continue;
                    if (WindowPlan.For(world, site).Style == FacadeStyle.Blank) continue;

                    Built far = Build(world, bridges, x, y, true);
                    Assert.Greater(far.Lit.Triangles.Count + far.Dark.Triangles.Count, 0, $"({x},{y}) has no glass at all");
                    checkedBuildings++;
                }
            }
            Assert.Greater(checkedBuildings, 20);
        }

        [Test]
        public void FarGlassIsOneSidedAndFloatsJustOffTheWall()
        {
            var world = new CityWorld(Seed);
            var bridges = new SkybridgeLayout(Seed);
            for (int x = -6; x <= 6; x++)
            {
                for (int y = -6; y <= 6; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!IsBuildingChunk(site, x, y)) continue;

                    Built far = Build(world, bridges, x, y, true);
                    Built full = Build(world, bridges, x, y, false);
                    // Full glass is two-sided (front and back copy of every pane); far glass is one face.
                    int farGlass = far.Lit.Triangles.Count + far.Dark.Triangles.Count;
                    int fullGlass = full.Lit.Triangles.Count + full.Dark.Triangles.Count;
                    Assert.LessOrEqual(farGlass, fullGlass, $"({x},{y}) far glass is heavier than full glass");
                }
            }
        }

        [Test]
        public void ABrokenPaneIsMissingFromFarGlassToo()
        {
            var world = new CityWorld(Seed);
            var bridges = new SkybridgeLayout(Seed);

            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    BuildingSite site = world.BuildingAt(x, y);
                    if (!IsBuildingChunk(site, x, y) || site.Params.Height < 30.0f) continue;

                    WindowPlan plan = WindowPlan.For(world, site);
                    CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                    CellLayout columns = plan.ColumnsFor(site.Params.Width);
                    for (int row = 2; row < rows.Count - 1; row++)
                    {
                        for (int column = 1; column < columns.Count - 1; column++)
                        {
                            if (!plan.IsGlazed(WindowGrid.Wall.North, column, row)) continue;

                            var damage = new GlassDamage();
                            Built before = Build(world, bridges, x, y, true, damage);
                            float areaBefore = GlassArea(before);
                            damage.Hit(new PaneKey(x, y, WindowGrid.Wall.North, column, row), null, shatterOutright: true);
                            Built after = Build(world, bridges, x, y, true, damage);

                            Assert.Less(GlassArea(after), areaBefore - 0.5f,
                                $"({x},{y}) column {column} row {row}: a shattered window is still drawn in the far version");
                            return;
                        }
                    }
                }
            }
            Assert.Fail("no building with a suitable window found");
        }

        private static float GlassArea(Built built)
        {
            float area = 0.0f;
            foreach (MeshBuffer buffer in new[] { built.Lit, built.Dark })
            {
                for (int t = 0; t + 2 < buffer.Triangles.Count; t += 3)
                {
                    Vector3 a = buffer.Vertices[buffer.Triangles[t]];
                    Vector3 b = buffer.Vertices[buffer.Triangles[t + 1]];
                    Vector3 c = buffer.Vertices[buffer.Triangles[t + 2]];
                    area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                }
            }
            return area;
        }
    }
}
