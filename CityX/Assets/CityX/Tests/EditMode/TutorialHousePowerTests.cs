using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The house's power (docs/OLD_GUYS_HOUSE.md): off until the fuse box's lever is thrown, then on for good; told once; forgotten for a new world.</summary>
    public sealed class TutorialHousePowerTests
    {
        [Test]
        public void OffUntilTurnedOnThenOnForGoodAndToldOnce()
        {
            var power = new TutorialHousePower();
            int told = 0;
            bool last = false;
            power.Changed += on => { told++; last = on; };
            Assert.IsFalse(power.IsOn, "the house should be dark to begin with");
            power.TurnOn();
            Assert.IsTrue(power.IsOn);
            Assert.AreEqual(1, told);
            Assert.IsTrue(last);
            power.TurnOn();
            Assert.AreEqual(1, told, "turning it on again should say nothing");
            power.Reset();
            Assert.IsFalse(power.IsOn);
            Assert.AreEqual(2, told);
            Assert.IsFalse(last);
            power.Reset();
            Assert.AreEqual(2, told, "resetting a dark house should say nothing");
        }
    }
}
