using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// World Gacha phase 3, the finer street detail: window canopies (庇) over the upper windows of homes (FacadeOrnament.CanopyDepth), shops with
    /// their roller shutters down and the band of shop signs over the shopfronts (World/Shopfronts, View/Buildings/CityMeshBuilder.Shopfronts.cs).
    /// </summary>
    public sealed class StreetDetailTests
    {
        private const int Seed = 48398181;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static FacadeOrnament OrnamentOf(BuildingSite site, out WindowPlan plan)
        {
            plan = WindowPlan.For(World, site);
            return FacadeOrnament.For(site, plan.Style, BuildingLook.For(site, plan.Style).Wall);
        }

        private sealed class Built
        {
            public readonly MeshBuffer Structures = new MeshBuffer(), Concrete = new MeshBuffer(), Lit = new MeshBuffer(), Dark = new MeshBuffer();
            public Vector3 Origin;
        }

        private static Built Build(BuildingSite site)
        {
            var built = new Built { Origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize) };
            CityMeshBuilder.BuildSite(World, site, built.Origin, built.Structures, built.Concrete, built.Lit, built.Dark, new MeshBuffer(), null, null,
                new MeshBuffer(), false);
            return built;
        }

        /// <summary>Does the segment from <paramref name="p0"/> to <paramref name="p1"/> (world) cross any triangle of the buffer?</summary>
        private static bool Crosses(MeshBuffer b, Vector3 origin, Vector3 p0, Vector3 p1)
        {
            return Crossed(b, origin, p0, p1) != null;
        }

        /// <summary>The first triangle of the buffer the segment crosses (its corners, world), or null.</summary>
        private static string Crossed(MeshBuffer b, Vector3 origin, Vector3 p0, Vector3 p1)
        {
            p0 -= origin;
            p1 -= origin;
            Vector3 dir = p1 - p0;
            for (int i = 0; i + 2 < b.Triangles.Count; i += 3)
            {
                Vector3 a = b.Vertices[b.Triangles[i]];
                Vector3 e1 = b.Vertices[b.Triangles[i + 1]] - a, e2 = b.Vertices[b.Triangles[i + 2]] - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1.0f / det;
                Vector3 s = p0 - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0.0f || u > 1.0f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv;
                if (v < 0.0f || u + v > 1.0f) continue;
                float t = Vector3.Dot(e2, q) * inv;
                if (t >= 0.0f && t <= 1.0f) return $"triangle {a + origin} {b.Vertices[b.Triangles[i + 1]] + origin} {b.Vertices[b.Triangles[i + 2]] + origin}";
            }
            return null;
        }

        /// <summary>A world point on a wall's outer face: <paramref name="u"/> along it from its start (the builder's column 0), <paramref name="y"/> above the base.</summary>
        private static Vector3 OnWall(BuildingSite site, WindowGrid.Wall wall, float u, float y, out Vector3 outward)
        {
            outward = CityMeshBuilder.WallNormal(site, wall);
            float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
            double lx, lz;
            switch (wall)
            {
                case WindowGrid.Wall.North: lx = -hw + u; lz = hd; break;
                case WindowGrid.Wall.South: lx = -hw + u; lz = -hd; break;
                case WindowGrid.Wall.East: lx = hw; lz = -hd + u; break;
                default: lx = -hw; lz = -hd + u; break;
            }
            site.LocalToWorld(lx, lz, out double wx, out double wz);
            return new Vector3((float)wx, site.BaseHeight + y, (float)wz);
        }

        // ---- window canopies ----

        [Test]
        public void WindowCanopiesAreCommonOnHomesAndNeverWithLintelsOrOnGlass()
        {
            var could = new Dictionary<ZoneKind, int>();
            var have = new Dictionary<ZoneKind, int>();
            foreach (BuildingSite site in TestSites.NearSpawn(World, 60))
            {
                if (!site.Exists || site.Params.IsSpecial) continue;
                FacadeOrnament o = OrnamentOf(site, out WindowPlan plan);
                if (o.HasCanopies)
                {
                    Assert.IsFalse(o.Lintels, $"building {site.Id}: canopies and lintels both");
                    Assert.That(plan.Style, Is.EqualTo(FacadeStyle.Punched).Or.EqualTo(FacadeStyle.Scattered), $"building {site.Id}: canopies on a {plan.Style} facade");
                    Assert.That(o.CanopyDepth, Is.InRange(FacadeOrnament.MinCanopyDepth, FacadeOrnament.MaxCanopyDepth));
                    Assert.That(o.CanopyDepth, Is.LessThan(CityX.Character.ClimbJut.MaxLean), "a canopy deeper than a climb leans round");
                }
                if (plan.Style != FacadeStyle.Punched && plan.Style != FacadeStyle.Scattered || site.OnRoof) continue;
                could[site.Zone] = (could.TryGetValue(site.Zone, out int c) ? c : 0) + 1;
                have[site.Zone] = (have.TryGetValue(site.Zone, out int h) ? h : 0) + (o.HasCanopies ? 1 : 0);
            }
            float Share(ZoneKind z) => could.TryGetValue(z, out int n) && n > 0 ? have[z] / (float)n : -1.0f;
            Debug.Log($"[Canopies] residential {Share(ZoneKind.Residential):F2} of {(could.TryGetValue(ZoneKind.Residential, out int r) ? r : 0)}, " +
                $"old town {Share(ZoneKind.OldTown):F2}, urban {Share(ZoneKind.Urban):F2}, core {Share(ZoneKind.Core):F2}");
            Assert.That(could.TryGetValue(ZoneKind.Residential, out int res) ? res : 0, Is.GreaterThan(40), "need a residential sample");
            Assert.That(Share(ZoneKind.Residential), Is.InRange(0.3f, 0.6f), "a good share of homes have canopies");
            Assert.That(Share(ZoneKind.Residential), Is.GreaterThan(Share(ZoneKind.Core)), "more on homes than downtown");
        }

        [Test]
        public void ACanopyHoodsEachUpperWindowAndNotTheStreetStorey()
        {
            Assert.Greater(FacadeOrnament.MinCanopyDepth, FacadeOrnament.BaseCourseDepth + 0.05f, "a canopy no deeper than the base course: the probe below cannot tell them apart");
            int checkedWindows = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 60))
            {
                if (!site.Exists || site.Params.IsSpecial || site.OnRoof || site.Sides >= 3) continue;
                FacadeOrnament o = OrnamentOf(site, out WindowPlan plan);
                if (!o.HasCanopies) continue;
                // A wall without balconies, a walkway, a party wall or another block against it.
                WindowGrid.Wall wall = WindowGrid.Wall.West;
                if (plan.IsBlankWall(wall) || site.Contacts != null && site.Contacts.Length > 0) continue;
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Depth);
                Built built = Build(site);
                for (int row = 0; row < Mathf.Min(rows.Count, 4); row++)
                {
                    for (int column = 0; column < columns.Count; column++)
                    {
                        if (!plan.IsGlazed(wall, column, row)) continue;
                        plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height), out _, out float insetTop);
                        float head = (row + 1) * rows.Size - rows.Size * insetTop;
                        float u = (column + 0.5f) * columns.Size;
                        Vector3 at = OnWall(site, wall, u, head, out Vector3 outward);
                        // A vertical segment through where its canopy is (no higher: over the street storey's windows the base course runs a few
                        // tens of centimetres up), out from the wall past the base course but under the canopy (seed 70485213: a building whose
                        // windows reach to 16 cm under the storey's top has its base course 6 cm over their heads, 24 cm proud).
                        Vector3 front = at + outward * Mathf.Max(o.CanopyDepth * 0.5f, FacadeOrnament.BaseCourseDepth + 0.05f);
                        string crossed = Crossed(built.Structures, built.Origin, front + Vector3.down * 0.1f,
                            front + Vector3.up * (FacadeOrnament.CanopyGap + FacadeOrnament.CanopyThickness + 0.02f));
                        Assert.AreEqual(row >= 1, crossed != null, $"building {site.Id} {wall} column {column} row {row} (head {site.BaseHeight + head:F2}, " +
                            $"{front}): {(row >= 1 ? "no canopy over an upper window" : "something over the street storey: " + crossed)}");
                        checkedWindows++;
                    }
                }
                if (checkedWindows > 60) break;
            }
            Assert.That(checkedWindows, Is.GreaterThan(20), "need canopied windows to check");
        }

        // ---- shutters and signs ----

        [Test]
        public void SomeShopsHaveTheirShutterDownAndThereIsNoWindowBehindIt()
        {
            int shops = 0, shuttered = 0, bays = 0, shutteredBays = 0, meshChecked = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 40))
            {
                if (!site.Exists || site.Params.IsSpecial || site.OnRoof || site.Sides >= 3) continue;
                WindowPlan plan = WindowPlan.For(World, site);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                if (!plan.Storefront)
                {
                    for (int c = 0; c < columns.Count; c++)
                    {
                        Assert.IsFalse(Shopfronts.Shuttered(site, plan, c), $"building {site.Id}: a shutter without a shop");
                    }
                    continue;
                }
                shops++;
                bool any = false;
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                for (int c = 0; c < columns.Count; c++)
                {
                    if (!plan.IsGlazed(WindowGrid.Wall.North, c, 0) || GlassDamage.IsDoorCell(site, WindowGrid.Wall.North, columns, c, 0)) continue;
                    bays++;
                    bool down = Shopfronts.Shuttered(site, plan, c);
                    shutteredBays += down ? 1 : 0;
                    any |= down;
                    Vector3 middle = OnWall(site, WindowGrid.Wall.North, (c + 0.5f) * columns.Size, rows.Size * 0.45f, out Vector3 outward);
                    if (down)
                    {
                        Assert.IsFalse(GlassDamage.TryPaneAt(World, site, middle - outward * 0.1f, out PaneKey _), $"building {site.Id} bay {c}: a pane behind a shutter");
                    }
                    if (down && meshChecked < 20)
                    {
                        // Shut: the curtain stands in the bay, and the shop's wall behind it is whole.
                        Built built = Build(site);
                        Assert.IsTrue(Crosses(built.Structures, built.Origin, middle + outward * 0.5f, middle - outward * 0.12f),
                            $"building {site.Id} bay {c}: no shutter in a shuttered bay");
                        Assert.IsTrue(Crosses(built.Concrete, built.Origin, middle + outward * 0.5f, middle - outward * (BuildingInterior.WallThickness + 0.2f)),
                            $"building {site.Id} bay {c}: a hole in the shop's inside wall behind its shutter");
                        meshChecked++;
                    }
                }
                shuttered += any ? 1 : 0;
            }
            Debug.Log($"[Shutters] {shuttered} of {shops} shopfronts with a shutter down, {shutteredBays} of {bays} bays");
            Assert.That(shops, Is.GreaterThan(30), "need a sample of shopfronts");
            Assert.That(shuttered / (float)shops, Is.InRange(0.15f, 0.45f), "about three shopfronts in ten have a shutter down");
            Assert.That(meshChecked, Is.GreaterThan(0));
        }

        [Test]
        public void TheShopSignsAreManyAndSomeAreLit()
        {
            int fronts = 0, lit = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 30))
            {
                if (!site.Exists || site.Params.IsSpecial || site.OnRoof || site.Sides >= 3) continue;
                WindowPlan plan = WindowPlan.For(World, site);
                if (!plan.Storefront || ApartmentFront.For(site, plan).Walkway) continue;
                fronts++;
                Built built = Build(site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                Vector3 outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                // A sign lit from behind: a glass face in the sign band over the shopfront, a few centimetres proud of the street wall -- lit glass
                // on a building with power, dark glass on one without (PowerGrid; NightLightingTests.ADeadBuildingsSignsAreDark).
                MeshBuffer faces = PowerGrid.IsOn(site) ? built.Lit : built.Dark;
                Vector3 wallPoint = OnWall(site, WindowGrid.Wall.North, 0.0f, 0.0f, out _);
                float bandBottom = site.BaseHeight + rows.Size + 0.12f, bandTop = site.BaseHeight + rows.Size + 0.78f;
                for (int i = 0; i < faces.Vertices.Count; i++)
                {
                    Vector3 v = faces.Vertices[i] + built.Origin;
                    float proud = Vector3.Dot(v - wallPoint, outward);
                    if (v.y > bandBottom && v.y < bandTop && proud > 0.05f && proud < 0.15f)
                    {
                        lit++;
                        break;
                    }
                }
                if (fronts >= 40) break;
            }
            Debug.Log($"[Signs] {lit} of {fronts} shopfronts with a lit sign");
            Assert.That(fronts, Is.GreaterThan(15), "need a sample of shopfronts");
            Assert.That(lit, Is.GreaterThan(fronts / 4), "few shopfronts have a lit sign");
        }
    }
}
