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
        private const int Seed = 39271846;

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
