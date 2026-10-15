using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// A tower on a podium (or a stage of a stepped tower) is an ATTACHMENT to the building, not a second building stacked on it: no shopfront, awnings or signs on its base,
    /// and its door is not a street entrance. Reported as "two layers of building on top of each other".
    /// </summary>
    public sealed class StackedVolumeTests
    {
        private const int Seed = 39271846;

        [Test]
        public void OnlyTheBottomVolumeOfABuildingIsAtStreetLevel()
        {
            var world = new CityWorld(Seed);
            int stacked = 0, bottoms = 0;
            for (int i = 0; i < world.BuildingCount; i++)
            {
                BuildingSite site = world.Building(i);
                if (!site.Exists) continue;
                // A volume stood on another one's roof starts a hair above that roof's top, not on the ground (see PlanWorld).
                if (site.OnRoof)
                {
                    stacked++;
                    Assert.AreEqual(site.BaseHeight, site.PodiumBase, 1e-4f, $"#{site.Id}: a volume on a roof has no podium skirt of its own");
                }
                else
                {
                    bottoms++;
                }
            }
            Assert.Greater(stacked, 100, "no stacked volumes at all: the massing has gone");
            Assert.Greater(bottoms, stacked / 3);
        }

        [Test]
        public void NoVolumeOnARoofHasAShopfront()
        {
            var world = new CityWorld(Seed);
            int checkedVolumes = 0;
            for (int i = 0; i < world.BuildingCount; i++)
            {
                BuildingSite site = world.Building(i);
                if (!site.Exists || !site.OnRoof) continue;
                checkedVolumes++;
                Assert.IsFalse(WindowPlan.For(world, site).Storefront, $"#{site.Id}: a tower standing on a roof was given a shopfront");
            }
            Assert.Greater(checkedVolumes, 100);
        }

        [Test]
        public void StreetLevelBuildingsStillGetShopfronts()
        {
            var world = new CityWorld(Seed);
            int shops = 0;
            for (int i = 0; i < world.BuildingCount && shops == 0; i++)
            {
                BuildingSite site = world.Building(i);
                if (site.Exists && !site.OnRoof && WindowPlan.For(world, site).Storefront) shops++;
            }
            Assert.Greater(shops, 0, "the fix took the shopfronts off the street too");
        }

        [Test]
        public void StripAShopfrontAndEveryPaneStillLinesUp()
        {
            // The plan without a shopfront keeps the same column grid, so the panes the mesh builder cuts and the panes a punch looks for agree.
            var world = new CityWorld(Seed);
            int compared = 0;
            for (int i = 0; i < world.BuildingCount && compared < 40; i++)
            {
                BuildingSite site = world.Building(i);
                if (!site.Exists) continue;
                WindowPlan with = CityX.World.WindowGrid.PlanFor(site.Seed, site.Zone, site.Params);
                if (!with.Storefront) continue;
                WindowPlan without = with.WithoutStorefront();
                Assert.IsFalse(without.Storefront);
                Assert.AreEqual(with.ColumnMetres, without.ColumnMetres, 1e-4f, "the column width changed");
                Assert.AreEqual(with.Style, without.Style);
                compared++;
            }
            Assert.Greater(compared, 5);
        }
    }
}
