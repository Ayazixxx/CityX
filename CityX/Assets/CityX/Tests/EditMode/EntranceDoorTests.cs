using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Pairs of front doors (owner, 2026-10-15: "15% percent of buildings have double doors as their main front entrance"): which buildings get
    /// one, and that the two leaves fill the wider doorway the wall builder cuts, standing in one plane and hanging toward each other.
    /// </summary>
    public sealed class EntranceDoorTests
    {
        private const int Seed = 63428175;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        [Test]
        public void AboutOneBuildingInSevenHasAPairOfFrontDoors()
        {
            int eligible = 0, pairs = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 30))
            {
                if (!site.Exists || site.Shell || site.OnRoof || site.Params.IsSpecial || site.Sides >= 3
                    || site.Params.Width < BuildingInterior.DoubleDoorWidth + 4.0f)
                {
                    Assert.IsFalse(BuildingInterior.HasDoubleEntrance(site), $"building {site.Id}: a pair of doors where there is no ordinary street door");
                    continue;
                }
                eligible++;
                bool pair = BuildingInterior.HasDoubleEntrance(site);
                pairs += pair ? 1 : 0;
                Assert.AreEqual(pair ? BuildingInterior.DoubleDoorWidth : BuildingInterior.DoorWidth, BuildingInterior.EntranceWidth(site), $"building {site.Id}");
            }
            float share = pairs / (float)Mathf.Max(1, eligible);
            Debug.Log($"[EntranceDoor] {pairs} pairs of front doors among {eligible} buildings ({share:P0})");
            Assert.Greater(eligible, 200, "need a real sample");
            Assert.That(share, Is.InRange(0.10f, 0.20f), "about 15 % of front entrances are a pair of doors");
        }

        [Test]
        public void ThePairFillsTheDoorwayInOnePlaneAndHangsTowardTheMiddle()
        {
            int checkedPairs = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 30))
            {
                BuildingDoor.Panel[] panels = BuildingDoor.PanelsFor(site);
                if (!BuildingInterior.HasDoubleEntrance(site))
                {
                    if (site.Exists && !site.Shell)
                    {
                        Assert.AreEqual(1, panels.Length, $"building {site.Id}: a single doorway with two leaves");
                    }
                    continue;
                }
                Assert.AreEqual(2, panels.Length, $"building {site.Id}");
                BuildingDoor.Panel left = panels[0], right = panels[1];
                Assert.AreEqual(BuildingInterior.DoubleDoorWidth, left.Width + right.Width, 1e-4f, "the leaves do not fill the doorway");

                // In the building's own frame: hinges at the doorway's two edges, a door's thickness apart in depth, facing opposite ways.
                site.WorldToLocal(left.Hinge.x, left.Hinge.z, out double lx, out double lz);
                site.WorldToLocal(right.Hinge.x, right.Hinge.z, out double rx, out double rz);
                Assert.AreEqual(-BuildingInterior.DoubleDoorWidth * 0.5, lx, 1e-3, "the left leaf's hinge is not at the doorway's left edge");
                Assert.AreEqual(BuildingInterior.DoubleDoorWidth * 0.5, rx, 1e-3, "the right leaf's hinge is not at the doorway's right edge");
                Assert.AreEqual(BuildingDoor.Thickness, lz - rz, 1e-3, "the right leaf's hinge must be a thickness further in, so the slabs share a plane");
                Assert.Less(Vector3.Dot(left.Facing, right.Facing), -0.99f, "the right leaf must face in, to hang back toward the middle");

                // Each leaf's free edge (the +X side of its hinge, where it faces) is at the middle of the doorway.
                Vector3 leftFree = left.Hinge + Vector3.Cross(Vector3.up, left.Facing) * left.Width;
                Vector3 rightFree = right.Hinge + Vector3.Cross(Vector3.up, right.Facing) * right.Width;
                site.WorldToLocal(leftFree.x, leftFree.z, out double lfx, out _);
                site.WorldToLocal(rightFree.x, rightFree.z, out double rfx, out _);
                Assert.AreEqual(0.0, lfx, 1e-3, "the left leaf does not reach the middle");
                Assert.AreEqual(0.0, rfx, 1e-3, "the right leaf does not reach the middle");
                if (++checkedPairs >= 10)
                {
                    break;
                }
            }
            Assert.Greater(checkedPairs, 3, "no pairs of doors near the spawn to check");
        }

        [Test]
        public void TheWindowsBesideAPairOfDoorsAreLeftClearOfIt()
        {
            // The wall builder cuts the wider doorway, and a window cell the doorway passes through gets no glass: GlassDamage asks the same width.
            int checkedPairs = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 30))
            {
                if (!BuildingInterior.HasDoubleEntrance(site))
                {
                    continue;
                }
                CellLayout columns = WindowPlan.For(World, site).ColumnsFor(site.Params.Width);
                float half = BuildingInterior.DoubleDoorWidth * 0.5f;
                for (int c = 0; c < columns.Count; c++)
                {
                    float u0 = -site.Params.Width * 0.5f + c * columns.Size;
                    bool throughTheDoorway = u0 + columns.Size > -half && u0 < half;
                    Assert.AreEqual(throughTheDoorway, GlassDamage.IsDoorCell(site, WindowGrid.Wall.North, columns, c, 0),
                        $"building {site.Id}: column {c} ({u0:F2} to {u0 + columns.Size:F2}) and a doorway {BuildingInterior.DoubleDoorWidth} m wide");
                }
                if (++checkedPairs >= 10)
                {
                    break;
                }
            }
            Assert.Greater(checkedPairs, 3, "no pairs of doors near the spawn to check");
        }
    }
}
