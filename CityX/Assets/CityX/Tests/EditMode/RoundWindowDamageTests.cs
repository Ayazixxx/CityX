using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A round tower's windows break the way a box building's do (owner, 2026-10-15: "the windows on the round buildings are not fracturing in the
    /// new improved way like the square buildings"): a shot at a pane is found as that pane, makes a hole in it with a crack decal round it, and
    /// the tower is drawn with the hole and the decal; the fifth shot brings the pane down.
    /// </summary>
    public sealed class RoundWindowDamageTests
    {
        private const int Seed = 46283197;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private sealed class Shot
        {
            public BuildingSite Site;
            public PaneKey Key;
            public Vector3 Point;
        }

        /// <summary>A pane on side 0 of a round tower (or the north wall of a box) a row or two up, with the world point at its middle on the glass.</summary>
        private static Shot FindPane(bool round)
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
                    for (int column = 1; column < columns.Count - 1; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row)) continue;
                        // The pane's middle, GlassSetBack in from the wall's face, in the builder's frame; side 0 of a round tower faces +Z like a
                        // box's north wall, its column 0 half a side back from the middle (GlassDamage.RoundAlong).
                        float u = (column + 0.5f) * columns.Size - sideLength * 0.5f;
                        float y = site.BaseHeight + (row + 0.5f) * rows.Size;
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

        private static void Build(BuildingSite site, GlassDamage damage, MeshBuffer cracked, MeshBuffer decals, List<CityMeshBuilder.CrackedOpening> openings)
        {
            var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
            CityMeshBuilder.BuildSite(World, site, origin, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), cracked, damage, openings,
                decals, false);
        }

        [TestCase(false, TestName = "ABoxBuildingsPaneTakesABulletHoleAndACrackDecal")]
        [TestCase(true, TestName = "ARoundTowersPaneTakesABulletHoleAndACrackDecal")]
        public void APaneTakesABulletHoleAndACrackDecal(bool round)
        {
            Shot shot = FindPane(round);
            Assert.IsNotNull(shot, round ? "no round tower near the spawn with a pane a shot finds" : "no box building near the spawn with a pane a shot finds");
            string what = $"building {shot.Site.Id} ({(round ? shot.Site.Sides + " sides" : "box")}) {shot.Key.Wall} column {shot.Key.Column} row {shot.Key.Row}";

            var damage = new GlassDamage();
            Vector2 uv = GlassDamage.FractionWithinPane(World, shot.Site, shot.Key, shot.Point);
            Assert.That(uv.x, Is.InRange(0.3f, 0.7f), $"{what}: the shot at the pane's middle is not in its middle across");
            Assert.That(uv.y, Is.InRange(0.3f, 0.7f), $"{what}: the shot at the pane's middle is not in its middle up");
            Assert.AreEqual(PaneState.Cracked, damage.Shoot(shot.Key, uv, out bool holed), what);
            Assert.IsTrue(holed, $"{what}: the first bullet made no hole");

            var cracked = new MeshBuffer();
            var decals = new MeshBuffer();
            var openings = new List<CityMeshBuilder.CrackedOpening>();
            Build(shot.Site, damage, cracked, decals, openings);
            Assert.That(cracked.Triangles.Count, Is.GreaterThan(6), $"{what}: the cracked pane is not drawn as glass round a hole (a whole pane is two triangles)");
            Assert.That(decals.Triangles.Count, Is.GreaterThanOrEqualTo(6), $"{what}: no crack decal round the hole");
            Assert.AreEqual(1, openings.Count, $"{what}: the cracked pane has no trigger box to knock it through");
            // The decal is drawn where the shot landed.
            var origin = new Vector3(shot.Site.OwnerChunkX * CityLayout.ChunkSize, 0, shot.Site.OwnerChunkY * CityLayout.ChunkSize);
            float nearest = float.MaxValue;
            for (int i = 0; i < decals.Vertices.Count; i++)
            {
                nearest = Mathf.Min(nearest, Vector3.Distance(decals.Vertices[i] + origin, shot.Point));
            }
            Assert.That(nearest, Is.LessThan(1.0f), $"{what}: the crack decal is {nearest:F2} m from where the shot landed");

            // The fifth bullet brings it down: nothing cracked is drawn any more, and the pane is gone.
            for (int i = 1; i < GlassDamage.ShatteringBullet; i++)
            {
                damage.Shoot(shot.Key, uv, out _);
            }
            Assert.AreEqual(PaneState.Shattered, damage.StateOf(shot.Key), what);
            cracked.Clear();
            decals.Clear();
            openings.Clear();
            Build(shot.Site, damage, cracked, decals, openings);
            Assert.AreEqual(0, cracked.Triangles.Count, $"{what}: a shattered pane still drawn cracked");
        }
    }
}
