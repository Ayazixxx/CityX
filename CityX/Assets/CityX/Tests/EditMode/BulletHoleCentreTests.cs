using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A bullet hole is drawn exactly where the bullet hit the glass (owner, 2026-10-26: "make sure the center of the bullet hole that appears on
    /// the windows is right at the center of where the bullet hit. ive noticed it feels off sometimes"). The hit is measured across the window's
    /// whole cell and the glass is inset in it, so a shot away from a pane's middle is where a mismatch shows: it was drawn pulled toward the middle.
    /// </summary>
    public sealed class BulletHoleCentreTests
    {
        private const int Seed = 61256619;

        /// <summary>How far the drawn hole's centre may be from the hit, metres (the decal stands 3 mm proud of the glass).</summary>
        private const float Tolerance = 0.01f;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private sealed class Shot
        {
            public BuildingSite Site;
            public PaneKey Key;
            public Vector3 Point;
        }

        /// <summary>
        /// A point on the glass of a pane on the north wall (side 0 of a round tower) a row or two up, <paramref name="across"/> and
        /// <paramref name="up"/> of the way over the GLASS (not the cell) from its bottom-left corner.
        /// </summary>
        private static Shot FindShot(bool round, float across, float up)
        {
            foreach (BuildingSite site in TestSites.NearSpawn(World, 60))
            {
                if (!site.Exists || site.Shell || site.OnRoof || site.Params.IsSpecial || (site.Sides >= 3) != round || site.Params.Height < 12.0f) continue;
                WindowPlan plan = WindowPlan.For(World, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                float sideLength = round ? RoundFootprint.Of(site).SideLength : site.Params.Width;
                CellLayout columns = plan.ColumnsFor(sideLength);
                for (int row = 1; row < Mathf.Min(rows.Count, 4); row++)
                {
                    plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height), out float insetBottom, out float insetTop);
                    float paneY0 = (row + insetBottom) * rows.Size;
                    float paneY1 = (row + 1 - insetTop) * rows.Size;
                    for (int column = 1; column < columns.Count - 1; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row)) continue;
                        float paneU0 = (column + WindowGrid.PaneInset) * columns.Size;
                        float paneU1 = (column + 1 - WindowGrid.PaneInset) * columns.Size;
                        float u = Mathf.Lerp(paneU0, paneU1, across) - sideLength * 0.5f;
                        float y = site.BaseHeight + Mathf.Lerp(paneY0, paneY1, up);
                        float z = (round ? RoundFootprint.Of(site).Apothem : site.Params.Depth * 0.5f) - BuildingInterior.GlassSetBack;
                        site.LocalToWorld(u, z, out double wx, out double wz);
                        var point = new Vector3((float)wx, y, (float)wz);
                        if (GlassDamage.TryPaneAt(World, site, point, out PaneKey key) && key.Column == column && key.Row == row)
                        {
                            return new Shot { Site = site, Key = key, Point = point };
                        }
                    }
                }
            }
            return null;
        }

        [TestCase(false, 0.2f, 0.25f, TestName = "ABoxBuildingsBulletHoleIsDrawnWhereTheBulletHitLowLeft")]
        [TestCase(false, 0.8f, 0.75f, TestName = "ABoxBuildingsBulletHoleIsDrawnWhereTheBulletHitHighRight")]
        [TestCase(true, 0.2f, 0.25f, TestName = "ARoundTowersBulletHoleIsDrawnWhereTheBulletHitLowLeft")]
        [TestCase(true, 0.8f, 0.75f, TestName = "ARoundTowersBulletHoleIsDrawnWhereTheBulletHitHighRight")]
        public void TheBulletHoleIsDrawnWhereTheBulletHit(bool round, float across, float up)
        {
            Shot shot = FindShot(round, across, up);
            Assert.IsNotNull(shot, round ? "no round tower near the spawn with a pane a shot finds" : "no box building near the spawn with a pane a shot finds");
            string what = $"building {shot.Site.Id} {shot.Key.Wall} column {shot.Key.Column} row {shot.Key.Row}";

            var damage = new GlassDamage();
            damage.Shoot(shot.Key, GlassDamage.FractionWithinPane(World, shot.Site, shot.Key, shot.Point), out bool holed);
            Assert.IsTrue(holed, what);

            var decals = new MeshBuffer();
            var origin = new Vector3(shot.Site.OwnerChunkX * CityLayout.ChunkSize, 0, shot.Site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, shot.Site, origin, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                damage, new List<CityMeshBuilder.CrackedOpening>(), decals, false);
            Assert.AreEqual(4, decals.Vertices.Count, $"{what}: one bullet, one crack decal");

            Vector3 centre = Vector3.zero;
            foreach (Vector3 corner in decals.Vertices)
            {
                centre += (corner + origin) * 0.25f;
            }
            float off = Vector3.Distance(centre, shot.Point);
            Assert.Less(off, Tolerance, $"{what}: the hole is drawn {off * 100.0f:F1} cm from where the bullet hit");
        }
    }
}
