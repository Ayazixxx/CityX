using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The slingshot against the bow, on numbers alone (docs/SLINGSHOT.md; owner, 2026-10-30: "the starter ranged non magic weapon ... make the
    /// draw quicker than the bow"): quicker to draw, shorter and lighter, and no quicker to kill with by tapping than by drawing.
    /// </summary>
    public sealed class SlingshotRulesTests
    {
        [Test]
        public void ItDrawsQuickerThanTheBowAndHitsLighterOverAShorterReach()
        {
            DrawnProfile sling = DrawnProfile.Slingshot, bow = DrawnProfile.Bow;
            Assert.AreEqual(ItemRegistry.Slingshot, sling.ItemId);
            Assert.Less(sling.FullDrawSeconds, bow.FullDrawSeconds, "the draw is quicker than the bow's");
            Assert.Less(sling.FullDamage, bow.FullDamage, "a starter weapon: lighter than the bow");
            Assert.Less(sling.FullFlatRange, bow.FullFlatRange, "and shorter");
            Assert.Greater(sling.DamageFor(1.0f), sling.DamageFor(0.0f), "a full draw is still worth holding");
            Assert.Greater(sling.FlatRangeFor(1.0f), sling.FlatRangeFor(0.0f));
            Assert.AreEqual(sling.FullDamage, sling.DamageFor(5.0f), 1e-4f, "past full is full");
            Assert.AreSame(sling, DrawnProfile.For(DrawnKind.Slingshot));
            Assert.AreSame(bow, DrawnProfile.For((DrawnKind)99), "an unknown kind from another player is the bow's");
        }

        [Test]
        public void TappingItIsNoQuickerToKillWithThanDrawingItFully()
        {
            DrawnProfile sling = DrawnProfile.Slingshot;
            const float frame = 1.0f / 60.0f;
            float tapping = sling.DamageFor(0.0f) / (sling.ReloadSeconds + 2.0f * frame);
            float drawing = sling.FullDamage / (sling.FullDrawSeconds + sling.ReloadSeconds);
            Assert.Less(tapping, drawing, $"tapping does {tapping:0.0} a second against a full draw's {drawing:0.0}");
            Assert.AreEqual(0.0f, DrawnProfile.Bow.ReloadSeconds, "the bow's nock is its own draw (the quiver), as before");
        }
    }
}
