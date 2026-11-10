using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

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

        [Test]
        public void TheShaftShowsFromTheSideAndNotLookedAlongFromEitherEnd()
        {
            Assert.AreEqual(1.0f, FlashlightRules.ShaftShown(0.0f), 1e-6f, "from the side the shaft is not whole");
            Assert.AreEqual(0.0f, FlashlightRules.ShaftShown(1.0f), 1e-6f, "looking into the beam the shaft's wedges show as slabs");
            // The game's camera stands behind the hand and looks along the beam: there the wedges were a crumpled pale shape by the hand.
            Assert.AreEqual(0.0f, FlashlightRules.ShaftShown(-0.95f), 1e-6f, "from behind the hand the shaft's wedges show as slabs");
            float before = 1.0f;
            for (int i = 1; i <= 20; i++)
            {
                float along = i / 20.0f;
                float shown = FlashlightRules.ShaftShown(along);
                Assert.LessOrEqual(shown, before + 1e-6f, $"the shaft comes back {along:0.00} of the way to looking along the beam");
                Assert.AreEqual(shown, FlashlightRules.ShaftShown(-along), 1e-6f, "the shaft is not the same from behind as from in front");
                before = shown;
            }
        }

        [Test]
        public void TheGlowRoundTheTorchIsFarDimmerThanItsBeamAndReachesTheWallsBesideWhoeverCarriesIt()
        {
            // Owner, 2026-11-09: "the flashlight/ other light sources need to light up the darkness". The beam alone lit a ring of wall some
            // metres ahead and left everything beside the player black.
            Assert.Greater(FlashlightRules.GlowShare, 0.02f, "the glow is nothing beside the beam");
            Assert.Less(FlashlightRules.GlowShare, 0.15f, "the glow is a second beam: the dark round the torch is gone");
            Assert.Greater(FlashlightRules.GlowReach, 5.0f, "the glow does not reach back to the camera behind the player");
            Assert.Less(FlashlightRules.GlowReach, FlashlightRules.Range * 0.3f, "the glow reaches as far as the beam");
        }

        [Test]
        public void TheGlowIsAsStrongAsTheTorchsPlaceIsOutOfTheSkysLight()
        {
            // The glow is the beam come back off the walls round the torch. On the street there are none: there it was a pool of light at
            // the carrier's feet, by day too.
            Assert.AreEqual(0.0f, FlashlightRules.GlowIntensity(0.0f), 1e-6f, "the torch glows under the sky");
            float whole = FlashlightRules.GlowIntensity(1.0f);
            Assert.AreEqual(FlashlightRules.Intensity * FlashlightRules.GlowShare, whole, 1e-4f, "deep in a tunnel the glow is not whole");
            float neck = FlashlightRules.GlowIntensity(0.5f);
            Assert.Greater(neck, 0.0f, "half way down a manhole's neck the torch does not glow yet");
            Assert.Less(neck, whole, "half way down a manhole's neck the glow is whole already");
            Assert.AreEqual(whole, FlashlightRules.GlowIntensity(3.0f), 1e-6f, "the glow grows past whole");
        }

        [Test]
        public void TheLightGetsTheBeamAsItIsMeantNotAsTheTextureHoldsIt()
        {
            // The cookie's texture is an sRGB one, and a light decodes what it reads from it: a spill put in as a half lit the walls of a
            // tunnel as a fifth, which was nothing (owner, 2026-11-09: "the flashlight/ other light sources need to light up the darkness").
            Assert.IsTrue(GraphicsFormatUtility.IsSRGBFormat(FlashlightLook.Cookie.graphicsFormat),
                "the cookie is no longer an sRGB texture: CookieTexelAt must stop encoding what it holds");
            for (int i = 0; i <= 20; i++)
            {
                float r = i / 20.0f;
                Assert.AreEqual(FlashlightRules.CookieAt(r), Mathf.GammaToLinearSpace(FlashlightRules.CookieTexelAt(r)), 0.004f,
                    $"{r:0.00} of the way out the light does not get the beam as it is meant");
            }
            Assert.Greater(FlashlightRules.CookieTexelAt(0.6f), FlashlightRules.CookieAt(0.6f) * 1.5f, "the spill is held as it is, not encoded");
        }
    }
}
