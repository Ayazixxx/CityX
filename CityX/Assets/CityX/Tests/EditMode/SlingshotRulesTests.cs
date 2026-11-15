using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

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

        /// <summary>
        /// Owner, 2026-11-15: "make him pinch the pouch part with his index middle and thumb fingers": the thumb's pad across from the index's and
        /// the middle finger's with the pebble between (a pad's width, the thumb's and the pebble's apart), the ring and little fingers folded
        /// back into the palm short of them, and the pouch held amid the three.
        /// </summary>
        [Test]
        public void ThePinchHoldsThePebbleBetweenTheThumbAndTheFirstTwoFingers()
        {
            FingerAmounts pinch = PlayerLocomotion.SlingshotPinch;
            Vector3 thumb = HandGrip.TipInHand(4, pinch), index = HandGrip.TipInHand(0, pinch), middle = HandGrip.TipInHand(1, pinch);
            float pads = FusedBodyShape.FingerRadius + FusedBodyShape.ThumbRadius;
            float pebble = SlingshotRules.PebbleSize * FusedBodyShape.ReferenceHeight / BodyRig.StandingHeight;
            Assert.That(Vector3.Distance(thumb, index), Is.InRange(pads, pads + pebble + 0.01f), "the thumb and the index finger do not pinch the pebble");
            Assert.That(Vector3.Distance(thumb, middle), Is.InRange(pads, pads + pebble + 0.02f), "the thumb and the middle finger do not pinch the pebble");
            Assert.Less(HandGrip.TipInHand(2, pinch).x, index.x - 0.04f, "the ring finger reaches on with the pinching ones");
            Assert.Less(HandGrip.TipInHand(3, pinch).x, index.x - 0.04f, "the little finger reaches on with the pinching ones");
            Assert.Less(Vector3.Distance(PlayerLocomotion.SlingshotPinchInHand, (thumb + index + middle) / 3.0f), 1e-4f);
            Assert.Greater(PlayerLocomotion.SlingshotPinchInHand.x, FusedBodyShape.PalmLength * 0.5f, "the pouch is held in the palm, not between the fingertips");
        }

        [Test]
        public void TheSlingshotIsAThirdBiggerAndItsPouchRestsBetweenItsTips()
        {
            // Owner, 2026-11-15: "mayve scale up the slingshot model slightly it is pretty small".
            Assert.AreEqual(1.3f, SlingshotRules.ModelScale, 1e-4f);
            Assert.AreEqual(0.2f * SlingshotRules.ModelScale, SlingshotRules.PouchAbove, 1e-4f, "the pouch rests where the old fork's tips were");
            Assert.AreEqual(SlingshotRules.PouchAbove, DrawnProfile.Slingshot.Style.NockAbove, 1e-4f, "the body draws to another pouch than the fork has");
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
