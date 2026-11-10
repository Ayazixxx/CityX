using CityX.Weapons;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The flashlight's beam shape, with no scene (docs/FLASHLIGHT.md): a full hot spot melting into a spill that falls to nothing.</summary>
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
        public void TheBeamIsOneAreaOfLightThatOnlyFadesGoingOut()
        {
            // Owner, 2026-11-09: "it keeps reflecting as circles instead of just an area of light like most light sources in video games do".
            // A ring, a ripple or a hot spot with an edge each draws a circle on the wall the beam falls on.
            const int steps = 200;
            const float gentlestEdge = 0.02f;   // the most the beam may lose over one step (half a percent of its width)
            float before = FlashlightRules.CookieAt(0.0f);
            for (int i = 1; i <= steps; i++)
            {
                float r = i / (float)steps;
                float here = FlashlightRules.CookieAt(r);
                Assert.LessOrEqual(here, before + 1e-6f, $"the beam brightens again {r:0.000} of the way out: a ring");
                Assert.Less(before - here, gentlestEdge, $"the beam drops off an edge {r:0.000} of the way out: a circle");
                before = here;
            }
        }
    }
}
