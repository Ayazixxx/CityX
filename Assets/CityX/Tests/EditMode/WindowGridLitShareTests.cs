using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// How many windows are lit at night follows the zone: downtown glows, an
    /// industrial estate is nearly dark.
    /// </summary>
    public class WindowGridLitShareTests
    {
        private static double LitFraction(double share)
        {
            int lit = 0;
            int total = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                for (int column = 0; column < 40; column++)
                {
                    for (int row = 0; row < 40; row++)
                    {
                        total++;
                        if (WindowGrid.IsLit(seed * 7919, WindowGrid.Wall.North, column, row, share))
                        {
                            lit++;
                        }
                    }
                }
            }
            return lit / (double)total;
        }

        [Test]
        public void EveryZoneLightsAboutItsOwnShareOfWindows()
        {
            foreach (ZoneKind zone in System.Enum.GetValues(typeof(ZoneKind)))
            {
                double expected = WindowGrid.LitShareFor(zone);
                Assert.AreEqual(expected, LitFraction(expected), 0.02, $"{zone}");
            }
        }

        [Test]
        public void DowntownGlowsAndIndustryIsNearlyDark()
        {
            Assert.Greater(WindowGrid.LitShareFor(ZoneKind.Core), WindowGrid.LitShareFor(ZoneKind.Urban));
            Assert.Greater(WindowGrid.LitShareFor(ZoneKind.Urban), WindowGrid.LitShareFor(ZoneKind.Residential));
            Assert.Greater(WindowGrid.LitShareFor(ZoneKind.Residential), WindowGrid.LitShareFor(ZoneKind.OldTown));
            Assert.Greater(WindowGrid.LitShareFor(ZoneKind.OldTown), WindowGrid.LitShareFor(ZoneKind.Outskirts));
            Assert.Greater(WindowGrid.LitShareFor(ZoneKind.Outskirts), WindowGrid.LitShareFor(ZoneKind.Industrial));
        }

        /// <summary>Retuning a zone's share must not reshuffle its buildings: a window lit at a low share stays lit at a higher one.</summary>
        [Test]
        public void ALowerShareIsAlwaysASubsetOfAHigherOne()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                for (int column = 0; column < 30; column++)
                {
                    for (int row = 0; row < 30; row++)
                    {
                        if (WindowGrid.IsLit(seed, WindowGrid.Wall.West, column, row, 0.2))
                        {
                            Assert.IsTrue(WindowGrid.IsLit(seed, WindowGrid.Wall.West, column, row, 0.45));
                        }
                    }
                }
            }
        }

        [Test]
        public void TheDefaultOverloadKeepsTheOriginalShare()
        {
            for (int column = 0; column < 30; column++)
            {
                for (int row = 0; row < 30; row++)
                {
                    Assert.AreEqual(
                        WindowGrid.IsLit(77, WindowGrid.Wall.East, column, row, WindowGrid.LitShare),
                        WindowGrid.IsLit(77, WindowGrid.Wall.East, column, row));
                }
            }
        }
    }
}
