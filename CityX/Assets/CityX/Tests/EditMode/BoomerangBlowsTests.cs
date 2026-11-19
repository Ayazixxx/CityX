using CityX.Weapons;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>How many people one throw of a boomerang may hit (docs/BOOMERANG.md; owner, 2026-11-18).</summary>
    public sealed class BoomerangBlowsTests
    {
        [Test]
        public void WithNothingUnlockedTheFirstBodyItHitsIsItsLast()
        {
            var blows = new BoomerangBlows(0);
            Assert.IsFalse(blows.Spent, "it could hit no one");
            Assert.IsTrue(blows.Strike(), "it goes on after the first body it hit");
            Assert.IsTrue(blows.Spent);
        }

        [Test]
        public void EachPassUnlockedIsOneMoreBody()
        {
            var blows = new BoomerangBlows(2);
            Assert.IsFalse(blows.Strike());
            Assert.IsFalse(blows.Strike());
            Assert.IsTrue(blows.Strike(), "two passes: the third body is its last");
            Assert.IsTrue(blows.Strike(), "spent stays spent");
        }

        [Test]
        public void UnlimitedIsNeverSpent()
        {
            var blows = new BoomerangBlows(BoomerangBlows.Unlimited);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(blows.Strike());
            }
        }

        [Test]
        public void ANegativeNumberOfPassesIsNone()
        {
            var blows = new BoomerangBlows(-3);
            Assert.IsFalse(blows.Spent);
            Assert.IsTrue(blows.Strike());
        }
    }
}
