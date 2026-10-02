using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What a building's shape is: a stack of volumes (a tower on a podium, a crown that steps in), each its own building standing on the
    /// roof of the one below.
    /// </summary>
    public sealed class MassingTests
    {
        private const int Seed = 61472093;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static readonly BuildingMass[] Masses =
            { BuildingMass.LowRise, BuildingMass.MidRise, BuildingMass.Slab, BuildingMass.Tower, BuildingMass.Supertall };

        private static List<MassVolume> Split(int seed, BuildingMass mass, double width, double depth, double height, out bool podium)
        {
            return Massing.Split(ZoneKind.Core, mass, width, depth, height, new DeterministicRandom(seed), out podium);
        }

        [Test]
        public void TheVolumesAddUpToTheBuildingsHeight()
        {
            for (int i = 0; i < 400; i++)
            {
                BuildingMass mass = Masses[i % Masses.Length];
                double height = 20.0 + (i * 7) % 230;
                List<MassVolume> volumes = Split(i, mass, 26.0 + i % 14, 30.0 + i % 22, height, out _);
                double sum = 0.0;
                foreach (MassVolume v in volumes)
                {
                    sum += v.Height;
                }
                Assert.AreEqual(height, sum, 1e-6, $"seed {i} {mass}");
            }
        }

        [Test]
        public void EachVolumeStandsInsideTheOneBelowIt()
        {
            for (int i = 0; i < 600; i++)
            {
                List<MassVolume> volumes = Split(i, Masses[i % Masses.Length], 28.0 + i % 16, 32.0 + i % 20, 40.0 + (i * 11) % 220, out _);
                for (int v = 1; v < volumes.Count; v++)
                {
                    MassVolume lower = volumes[v - 1];
                    MassVolume upper = volumes[v];
                    Assert.GreaterOrEqual(upper.X0, lower.X0 - 1e-6, $"seed {i} volume {v}");
                    Assert.LessOrEqual(upper.X1, lower.X1 + 1e-6, $"seed {i} volume {v}");
                    Assert.GreaterOrEqual(upper.Z0, lower.Z0 - 1e-6, $"seed {i} volume {v}");
                    Assert.LessOrEqual(upper.Z1, lower.Z1 + 1e-6, $"seed {i} volume {v}");
                }
            }
        }

        [Test]
        public void NoVolumeIsAChimneyOrALedge()
        {
            for (int i = 0; i < 600; i++)
            {
                foreach (MassVolume v in Split(i, Masses[i % Masses.Length], 24.0 + i % 18, 28.0 + i % 24, 30.0 + (i * 13) % 240, out _))
                {
                    Assert.GreaterOrEqual(v.Width, Massing.MinSide - 1e-6, $"seed {i}");
                    Assert.GreaterOrEqual(v.Depth, Massing.MinSide - 1e-6, $"seed {i}");
                    // The one volume of a plain building is whatever height the lot gave it; a volume of a stack is at least two storeys.
                }
            }
        }

        [Test]
        public void EveryVolumeOfAStackIsAtLeastTwoStoreysTall()
        {
            for (int i = 0; i < 600; i++)
            {
                List<MassVolume> volumes = Split(i, Masses[i % Masses.Length], 30.0 + i % 14, 34.0 + i % 20, 60.0 + (i * 13) % 200, out _);
                if (volumes.Count < 2)
                {
                    continue;
                }
                foreach (MassVolume v in volumes)
                {
                    Assert.GreaterOrEqual(v.Height, Massing.MinHeight - 1e-6, $"seed {i}");
                }
            }
        }

        [Test]
        public void TheSameSeedAlwaysGivesTheSameShape()
        {
            List<MassVolume> a = Split(77, BuildingMass.Tower, 34.0, 40.0, 150.0, out bool pa);
            List<MassVolume> b = Split(77, BuildingMass.Tower, 34.0, 40.0, 150.0, out bool pb);
            Assert.AreEqual(pa, pb);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X0, b[i].X0);
                Assert.AreEqual(a[i].Z1, b[i].Z1);
                Assert.AreEqual(a[i].Height, b[i].Height);
            }
        }

        [Test]
        public void TowersUsuallyHaveAShapeAndLowRiseNeverDoes()
        {
            int stacked = 0, lowStacked = 0;
            for (int i = 0; i < 300; i++)
            {
                stacked += Split(i, BuildingMass.Tower, 36.0, 40.0, 150.0, out _).Count > 1 ? 1 : 0;
                lowStacked += Split(i, BuildingMass.LowRise, 36.0, 40.0, 15.0, out _).Count > 1 ? 1 : 0;
            }
            Assert.Greater(stacked, 240, "most towers are not plain boxes");
            Assert.AreEqual(0, lowStacked);
        }

        [Test]
        public void IndustrialAndOutlyingBuildingsStayPlain()
        {
            for (int i = 0; i < 200; i++)
            {
                Assert.AreEqual(1, Massing.Split(ZoneKind.Industrial, BuildingMass.Warehouse, 60.0, 80.0, 18.0, new DeterministicRandom(i), out _).Count);
            }
        }

        // ---- in the real city ----

        [Test]
        public void ACityOfBoxesIsNoMoreAFairShareOfTheBuildingsAreStacked()
        {
            int stacked = 0;
            int total = World.BuildingCount;
            for (int id = 0; id < total; id++)
            {
                stacked += World.Building(id).HasRoofObstacle ? 1 : 0;
            }
            float share = (float)stacked / total;
            Assert.Greater(share, 0.04f, "a skyline with a few shaped towers in it");
            Assert.Less(share, 0.6f, "but not every building");
        }

        [Test]
        public void AVolumeStandsOnTheRoofOfTheOneBelowItAndInsideItsOutline()
        {
            int checkedStacks = 0;
            for (int id = 0; id < World.BuildingCount - 1 && checkedStacks < 300; id++)
            {
                BuildingSite lower = World.Building(id);
                if (!lower.HasRoofObstacle)
                {
                    continue;
                }
                BuildingSite upper = World.Building(id + 1);
                checkedStacks++;

                Assert.AreEqual(lower.BaseHeight + lower.Params.Height + 0.05f, upper.BaseHeight, 0.01f, $"building {id}");
                Assert.AreEqual(upper.BaseHeight, upper.PodiumBase, 1e-4f, "it has no podium of its own: it stands on a roof");
                Assert.AreEqual(upper.BaseHeight, upper.DoorGroundHeight, 1e-4f, "and its door is level with it");
                Assert.AreEqual(lower.AxisX, upper.AxisX, 1e-9, "turned the same way");

                // Its centre, in the lower volume's frame, is inside the outline.
                lower.WorldToLocal(upper.CentreX, upper.CentreZ, out double lx, out double lz);
                Assert.LessOrEqual(Mathf.Abs((float)lx) + upper.Params.Width * 0.5f, lower.Params.Width * 0.5f + 0.01f, $"building {id}");
                Assert.LessOrEqual(Mathf.Abs((float)lz) + upper.Params.Depth * 0.5f, lower.Params.Depth * 0.5f + 0.01f, $"building {id}");

                // And the obstacle rectangle the lower volume records is exactly where the upper one is.
                Assert.AreEqual(lx - upper.Params.Width * 0.5, lower.ObstacleX0, 0.01);
                Assert.AreEqual(lz + upper.Params.Depth * 0.5, lower.ObstacleZ1, 0.01);
            }
            Assert.Greater(checkedStacks, 20, "need a real sample");
        }

        [Test]
        public void HeightDecidesWhichOfTwoStackedBuildingsAPointIsOn()
        {
            int tried = 0;
            for (int id = 0; id < World.BuildingCount - 1 && tried < 40; id++)
            {
                BuildingSite lower = World.Building(id);
                if (!lower.HasRoofObstacle)
                {
                    continue;
                }
                BuildingSite upper = World.Building(id + 1);
                tried++;

                Vector3 inLower = new Vector3((float)upper.CentreX, lower.BaseHeight + 2.0f, (float)upper.CentreZ);
                Vector3 inUpper = new Vector3((float)upper.CentreX, upper.BaseHeight + 2.0f, (float)upper.CentreZ);
                Assert.IsTrue(World.TryBuildingAt(inLower, out BuildingSite a, 0.0));
                Assert.IsTrue(World.TryBuildingAt(inUpper, out BuildingSite b, 0.0));
                Assert.AreEqual(lower.Id, a.Id, "low down it is the lower building");
                Assert.AreEqual(upper.Id, b.Id, "up on the roof it is the upper one");
            }
            Assert.Greater(tried, 10);
        }

        [Test]
        public void TheStairToARoofDoesNotComeOutUnderTheVolumeStandingOnIt()
        {
            int cleared = 0, blocked = 0;
            for (int id = 0; id < World.BuildingCount && cleared + blocked < 400; id++)
            {
                BuildingSite site = World.Building(id);
                if (!site.HasRoofObstacle)
                {
                    continue;
                }
                float halfX = site.Params.Width * 0.5f;
                float halfZ = site.Params.Depth * 0.5f;
                float run = BuildingInterior.RunFor(halfX, halfZ);
                int top = BuildingInterior.FloorCountFor(site.Params.Height) - 1;
                var avoid = Rect.MinMaxRect(site.ObstacleX0 - 0.4f, site.ObstacleZ0 - 0.4f, site.ObstacleX1 + 0.4f, site.ObstacleZ1 + 0.4f);

                BuildingInterior.Stairwell well = BuildingInterior.StairwellFor(top, halfX, halfZ, run, avoid);
                if (well.Hole.Overlaps(avoid))
                {
                    blocked++;
                }
                else
                {
                    cleared++;
                }
            }
            Assert.Greater(cleared + blocked, 20, "need a real sample");
            Assert.Greater(cleared, blocked * 2, "the roof can almost always be reached");
        }
    }
}
