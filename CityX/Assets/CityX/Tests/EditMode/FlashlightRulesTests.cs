using CityX.Weapons;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The flashlight's beam shape, with no scene (docs/FLASHLIGHT.md): a full hot spot, a ring past it, a spill falling to nothing.</summary>
    public sealed class FlashlightRulesTests
    {
        [Test]
        public void TheHotSpotIsFullTheSpillDimAndTheEdgeDark()
        {
            float hot = FlashlightRules.HotSpotShare;
            Assert.Greater(hot, 0.15f, "the hot spot is a pin prick");
            Assert.Less(hot, 0.5f, "the hot spot fills the beam");
            Assert.Greater(FlashlightRules.CookieAt(0.0f), 0.95f, "the middle of the beam is not full");
            Assert.Greater(FlashlightRules.CookieAt(hot * 0.5f), 0.9f, "the hot spot is not full across");
            float spill = FlashlightRules.CookieAt(0.6f);
            Assert.Greater(spill, 0.08f, "no spill round the hot spot");
            Assert.Less(spill, 0.45f, "the spill is as bright as the hot spot");
            Assert.Less(FlashlightRules.CookieAt(0.99f), 0.02f, "the beam's edge is not dark");
            Assert.AreEqual(0.0f, FlashlightRules.CookieAt(1.2f), 1e-6f);
        }

        [Test]
        public void TheReflectorsRingShowsJustPastTheHotSpot()
        {
            float hot = FlashlightRules.HotSpotShare;
            float ring = FlashlightRules.CookieAt(hot * 1.25f);
            Assert.Greater(ring, FlashlightRules.CookieAt(hot * 1.6f), "no ring past the hot spot");
        }
    }
}
