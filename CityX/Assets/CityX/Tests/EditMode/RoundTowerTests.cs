using System;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Round and many-sided towers (BuildingSite.Sides, RoundFootprint, CityMeshBuilder.BuildRound). See docs/BUILDING_SHAPES.md.</summary>
    public sealed class RoundTowerTests
    {
        private const int Seed = 80469103;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static readonly int[] SideCounts = { 6, 8, 12, 16, 20 };

        private static IEnumerable<BuildingSite> RoundSites(int max)
        {
            int n = 0;
            for (int id = 0; id < World.BuildingCount && n < max; id++)
            {
                BuildingSite s = World.Building(id);
                if (s.Sides >= 3)
                {
                    n++;
                    yield return s;
                }
            }
        }

        [Test]
        public void TheOutlineHasASideFacingTheFrontAndItsCornersOnTheCircle()
        {
            foreach (int sides in SideCounts)
            {
                var fp = new RoundFootprint(sides, 15.0f);
                fp.SideNormal(0, out float nx, out float nz);
                Assert.AreEqual(0.0f, nx, 1e-5f, $"{sides}: side 0 faces +Z");
                Assert.AreEqual(1.0f, nz, 1e-5f);
                for (int i = 0; i < sides; i++)
                {
                    fp.SideNormal(i, out nx, out nz);
                    Assert.AreEqual(i, fp.SideAt(nx * fp.Apothem, nz * fp.Apothem), $"{sides} sides, side {i}");
                    Assert.AreEqual(fp.Apothem, fp.BoundaryRadius(fp.SideAngle(i)), 1e-3f);
                    Assert.AreEqual(fp.Radius, fp.BoundaryRadius(fp.CornerAngle(i)), 1e-3f, "the corners are on the circle");
                }
                // The square for the stairs fits inside, corners and all.
                for (int q = 0; q < 4; q++)
                {
                    double a = Math.PI * 0.25 + q * Math.PI * 0.5;
                    Assert.Less(fp.InnerHalf * Mathf.Sqrt(2.0f), fp.BoundaryRadius(a), $"{sides} sides: the stair square pokes out");
                }
            }
        }

        [Test]
        public void TheCityHasRoundTowers()
        {
            int count = 0;
            var seen = new HashSet<int>();
            foreach (BuildingSite s in RoundSites(int.MaxValue))
            {
                count++;
                seen.Add(s.Sides);
                Assert.AreEqual(s.Params.Width, s.Params.Depth, 1e-4f, $"#{s.Id}: a round site is as wide as it is deep (the circle its corners are on)");
                Assert.Contains(s.Sides, SideCounts);
            }
            Debug.Log($"CityX round towers: {count} volumes, side counts {string.Join(",", seen)}");
            Assert.Greater(count, 40);
            Assert.GreaterOrEqual(seen.Count, 3, "more than one kind of round");
        }

        [Test]
        public void AShotAtAnyWindowOfARoundTowerFindsThatWindow()
        {
            int tried = 0;
            foreach (BuildingSite site in RoundSites(25))
            {
                if (site.Shell)
                {
                    continue;
                }
                var fp = RoundFootprint.Of(site);
                WindowPlan plan = WindowPlan.For(World, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(fp.SideLength);
                for (int side = 0; side < fp.Sides; side++)
                {
                    WindowGrid.Wall which = WindowGrid.PolygonSide(side);
                    fp.SideNormal(side, out float nx, out float nz);
                    for (int row = 0; row < Mathf.Min(rows.Count, 6); row++)
                    {
                        for (int col = 0; col < columns.Count; col++)
                        {
                            if (!plan.IsGlazed(which, col, row) || GlassDamage.IsDoorCell(site, which, columns, col, row))
                            {
                                continue;
                            }
                            float u = -fp.SideLength * 0.5f + (col + 0.5f) * columns.Size;
                            // On the outside face, a hair out: the side's middle plus u along it (the side's +X is (nz, -nx)).
                            float lx = nx * (fp.Apothem + 0.01f) + nz * u;
                            float lz = nz * (fp.Apothem + 0.01f) - nx * u;
                            site.LocalToWorld(lx, lz, out double wx, out double wz);
                            var point = new Vector3((float)wx, site.BaseHeight + (row + 0.5f) * rows.Size, (float)wz);
                            Assert.IsTrue(GlassDamage.TryPaneAt(World, site, point, out PaneKey key), $"#{site.Id} side {side} column {col} row {row}: no pane found");
                            Assert.AreEqual(new PaneKey(site.Id, which, col, row), key, $"#{site.Id} side {side}");
                            Vector2 f = GlassDamage.FractionWithinPane(World, site, key, point);
                            Assert.AreEqual(0.5f, f.x, 0.02f);
                            Assert.AreEqual(0.5f, f.y, 0.02f);
                            tried++;
                        }
                    }
                }
            }
            Assert.Greater(tried, 300);
        }

        [Test]
        public void ARoundTowersStairsStayInsideItsMiddleSquare()
        {
            int checkedSites = 0;
            foreach (BuildingSite site in RoundSites(60))
            {
                if (site.Shell)
                {
                    continue;
                }
                float h = RoundFootprint.Of(site).InnerHalf + 0.01f;
                BuildingStairs.Layout layout = BuildingStairs.PlanFor(site);
                foreach (StairPlan p in layout.Floors)
                {
                    if (p == null)
                    {
                        continue;
                    }
                    Assert.GreaterOrEqual(p.Bounds.xMin, -h, $"#{site.Id}");
                    Assert.LessOrEqual(p.Bounds.xMax, h, $"#{site.Id}");
                    Assert.GreaterOrEqual(p.Bounds.yMin, -h, $"#{site.Id}");
                    Assert.LessOrEqual(p.Bounds.yMax, h, $"#{site.Id}");
                }
                checkedSites++;
            }
            Assert.Greater(checkedSites, 10);
        }

        [Test]
        public void ARoundTowersMeshStaysOnItsCircleAndIsWellFormed()
        {
            int built = 0;
            foreach (BuildingSite site in RoundSites(12))
            {
                foreach (bool far in new[] { false, true })
                {
                    var origin = new Vector3((float)site.CentreX, 0.0f, (float)site.CentreZ);
                    var structures = new MeshBuffer();
                    var concrete = new MeshBuffer();
                    var lit = new MeshBuffer();
                    var dark = new MeshBuffer();
                    CityMeshBuilder.BuildSite(World, site, origin, structures, concrete, lit, dark, null, null, null, null, far);
                    float radius = site.Params.Width * 0.5f;
                    // A door up a slope has a flight of steps out to the street, as long as the climb needs.
                    BuildingDecor.EntranceStyle entrance = BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight);
                    float steps = entrance.NeedsStairs ? BuildingInterior.StepsFor(entrance.Rise) * CityMeshBuilder.StreetStairTreadDepth : 0.0f;
                    // Nothing under the podium but the footing of those steps: a folded stair's outer lanes stand on the paving further out in front of
                    // the door, lower down a slope (2026-10-16, EntranceStairs.PlanFolded: #110 on seed 19077151 reached 4.5 cm past half a metre under).
                    float lowest = site.PodiumBase - 0.5f;
                    if (entrance.NeedsStairs)
                    {
                        RoundFootprint fp = RoundFootprint.Of(site);
                        float half = fp.SideLength * 0.5f, reach = EntranceStairs.MaxLanes * EntranceStairs.Width;
                        for (float u = -half; u <= half + 1e-3f; u += half * 0.5f)
                        {
                            for (float o = 0.0f; o <= reach + 1e-3f; o += EntranceStairs.Width * 0.5f)
                            {
                                site.LocalToWorld(u, fp.Apothem + o, out double wx, out double wz);
                                float paving = (float)(World.Plan.GroundHeight(wx, wz) + CityX.World.Plan.PlanBuildings.PavingRise);
                                lowest = Mathf.Min(lowest, paving - EntranceStairs.Footing - 0.05f);
                            }
                        }
                    }
                    foreach (MeshBuffer b in new[] { structures, concrete, lit, dark })
                    {
                        foreach (Vector3 v in b.Vertices)
                        {
                            Assert.IsFalse(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), $"#{site.Id}: NaN");
                            // Within the circle, give or take the parapet's coping, awnings, signs and the entrance steps at the door.
                            Assert.LessOrEqual(new Vector2(v.x, v.z).magnitude, radius + 4.0f + steps, $"#{site.Id} far={far}: a vertex far off the circle");
                            Assert.GreaterOrEqual(v.y, lowest, $"#{site.Id}: below the podium (and its entrance stairs' footing)");
                            // Nothing over the tallest antenna mast, or over the tower's own top (World/TowerTop: a spire on a tall round tower).
                            Assert.LessOrEqual(v.y, site.Top + Mathf.Max(30.0f, TowerTop.For(site).Height + 1.0f), $"#{site.Id}: above the tallest antenna mast");
                        }
                        Assert.AreEqual(0, b.Triangles.Count % 3);
                    }
                    Assert.Greater(structures.Triangles.Count, 3 * RoundFootprint.Of(site).Sides, $"#{site.Id}: a wall per side at least");
                    if (!far && !site.Shell)
                    {
                        Assert.Greater(dark.Triangles.Count + lit.Triangles.Count, 0, $"#{site.Id}: no glass");
                    }
                    built++;
                }
            }
            Assert.Greater(built, 10);
        }
    }
}
