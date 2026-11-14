using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A block is paved as one surface from the kerb to its buildings' walls (CitySurfaceBuilder.BuildBlockPaving), at the pavement's height, and the
    /// ground floors stand on it (PlanBuildings.PavingRise) -- instead of a patchwork of 4 m ground squares with staircase edges below the pavement.
    /// </summary>
    public sealed class BlockPavingTests
    {
        private const int Seed = CitySeed.Default;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        [Test]
        public void ThePavingIsExactlyAsHighAsThePavement()
        {
            Assert.AreEqual(CitySurfaceBuilder.RoadLift + CitySurfaceBuilder.KerbHeight, (float)PlanBuildings.PavingRise, 1e-5f);
        }

        [Test]
        public void TriangulatingAConcaveOutlineCoversItExactly()
        {
            // An L: ear clipping, not a fan (a fan from a reflex corner would spill outside).
            var l = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 4), new Vec2(4, 4), new Vec2(4, 10), new Vec2(0, 10) };
            int[] t = PlanMath.Triangulate(l);
            Assert.AreEqual((l.Length - 2) * 3, t.Length);
            double area = 0.0;
            for (int k = 0; k < t.Length; k += 3)
            {
                double a = PlanMath.SignedArea(new[] { l[t[k]], l[t[k + 1]], l[t[k + 2]] });
                Assert.Greater(a, 0.0, "every triangle wound like the outline, none flipped or empty");
                area += a;
            }
            Assert.AreEqual(PlanMath.SignedArea(l), area, 1e-9);
        }

        /// <summary>
        /// Block 520 of seed 58203917 at the classic scale: a boulevard corner cut off by a 25 m stub of road, a sharp corner between a boulevard and
        /// a street. The kerb line's edge along the stub came out backwards (its ends crossed over at the sharp corner) and lay flat along the next
        /// edge, a fold the inset's checks never saw: the paving's triangles overlapped by a tenth. The folded edge is now collapsed to a point.
        /// </summary>
        [Test]
        public void AKerbEdgeShorterThanItsCornersMitreIsCollapsedNotFoldedOver()
        {
            var road = new[] { new Vec2(-1729.8804, 1857.1486), new Vec2(-1704.5717, 1858.5887), new Vec2(-1626.8624, 1863.0105), new Vec2(-1655.1552, 1906.1531) };
            foreach (double sidewalk in new[] { 0.0, 3.0 })
            {
                var distances = new[] { 10.0 + sidewalk, 10.0 + sidewalk, 5.5 + sidewalk, 5.5 + sidewalk };   // boulevard, boulevard, street, street
                var inset = new System.Collections.Generic.List<Vec2>();
                Assert.IsTrue(PlanMath.InsetPolygon(road, distances, inset, out _), $"the block is lost (sidewalk {sidewalk})");
                Assert.AreEqual(road.Length, inset.Count, "one corner per road corner, as the pavement ring pairs them");
                for (int i = 0; i < road.Length; i++)
                {
                    Vec2 along = (road[(i + 1) % road.Length] - road[i]).Normalized;
                    Assert.GreaterOrEqual(Vec2.Dot(inset[(i + 1) % inset.Count] - inset[i], along), -1e-9, $"edge {i} runs backwards (sidewalk {sidewalk})");
                }
                Assert.IsFalse(PlanMath.SelfIntersects(inset));
                int[] t = PlanMath.Triangulate(inset);
                double area = 0.0;
                for (int k = 0; k < t.Length; k += 3)
                {
                    double a = PlanMath.SignedArea(new[] { inset[t[k]], inset[t[k + 1]], inset[t[k + 2]] });
                    Assert.GreaterOrEqual(a, -1e-9, "a triangle wound against the outline: the fold is still there");
                    area += a;
                }
                Assert.AreEqual(PlanMath.SignedArea(inset), area, 1e-6, $"the paving does not cover the block exactly (sidewalk {sidewalk})");
                // The block is a thin triangle (2305 m2 of road outline) with two corners of about 30 degrees, each of which the inset
                // moves nearly 40 m along its bisector: its kerb line is 465 m2 and its lot line smaller still.
                Assert.Greater(area, 100.0, "the block has vanished");
                Assert.Less(area, 2305.0, "the inset is bigger than the road outline");
            }
        }

        [Test]
        public void EveryPavedBlockIsCoveredKerbToKerb()
        {
            int paved = 0;
            foreach (PlanBlock block in World.Plan.Data.Blocks)
            {
                if (!block.Paved)
                {
                    continue;
                }
                paved++;
                Assert.IsFalse(block.Encloses, "a block with an island inside would pave over it");
                double area = 0.0;
                int[] t = block.InnerTriangles;
                for (int k = 0; k < t.Length; k += 3)
                {
                    area += System.Math.Abs(PlanMath.SignedArea(new[] { block.Inner[t[k]], block.Inner[t[k + 1]], block.Inner[t[k + 2]] }));
                }
                double outline = System.Math.Abs(PlanMath.SignedArea(block.Inner));
                Assert.AreEqual(outline, area, outline * 1e-6 + 1e-6, "the paving leaves a hole or overlaps itself");
            }
            Assert.Greater(paved, 500, "need a real city");
        }

        [Test]
        public void EveryGroundFloorStandsOnThePavingNotUnderIt()
        {
            int checkedSites = 0;
            foreach (BuildingSite site in World.Plan.Buildings)
            {
                if (site.OnRoof)
                {
                    continue;
                }
                double hw = site.Params.Width * 0.5, hd = site.Params.Depth * 0.5;
                for (int k = 0; k < 4; k++)
                {
                    site.LocalToWorld((k & 1) == 0 ? -hw : hw, (k & 2) == 0 ? -hd : hd, out double x, out double z);
                    double paving = World.Plan.GroundHeight(x, z) + PlanBuildings.PavingRise;
                    Assert.GreaterOrEqual(site.BaseHeight, paving - 0.05, $"building {site.Id}: the paving comes up through its ground floor");
                }
                checkedSites++;
            }
            Assert.Greater(checkedSites, 1000);
        }
    }
}
