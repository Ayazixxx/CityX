using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE BO STAFF (owner, 2026-11-19, with a clip of a staff fighter: "make copy these movements as best you can", its opening spin "maybe that will
    /// be the heavy hold click attack"; "make the model just a basic metal pole"; docs/BO_STAFF.md): its six attacks and their clips, the chain's
    /// thrust keeping its line, the pole's far end off the ground through every attack, the low sweep at the shins, and the built pole.
    /// </summary>
    public sealed class BoStaffTests
    {
        private static readonly SwordStrikeKind[] Staff =
        {
            SwordStrikeKind.StaffChop, SwordStrikeKind.StaffSweep, SwordStrikeKind.StaffThrust, SwordStrikeKind.StaffRising, SwordStrikeKind.StaffSpin,
            SwordStrikeKind.StaffRun,
        };

        /// <summary>The far end, from the frame's wrist along the pole, in the body's standing frame lowered by the frame's sink, metres.</summary>
        private static Vector3 FarEnd(in SwordFrame frame) =>
            SwordStrikes.HandPoint(frame) + frame.Blade * (StaffRules.Tip + SwordWeapon.DefaultPrimaryGripDown) + Vector3.down * frame.HipDrop;

        [Test]
        public void SixAttacksEachWithItsClipTheSpinTheHeavyAndTheThrustOnTheRun()
        {
            foreach (SwordStrikeKind kind in Staff)
            {
                Assert.IsTrue(SwordStrikes.IsStaff(kind), kind + " is not a staff's");
                Assert.IsTrue(SwordStrikes.TryGet(SwordStrikes.NameOf(kind), out SwordStrikeKind back) && back == kind, kind + "'s name does not travel");
                Assert.IsTrue(SwordStrikes.HasClip(kind), kind + " has no clip: run SourceArt/SwordMoves/sword_moves_build.py");
                Assert.IsFalse(SwordStrikes.IsOneHanded(kind));
            }
            Assert.IsTrue(SwordStrikes.IsHeavy(SwordStrikeKind.StaffSpin), "the spin (the clip's opening blow) is not the heavy");
            Assert.Greater(SwordStrikes.TimingOf(SwordStrikeKind.StaffSpin).Hold, 0.25f, "the heavy is not held wound up long enough to read");
            Assert.AreEqual(SwordStrikeKind.StaffRun, SwordStrikes.StaffOpenerFor(7.5f));
            Assert.AreEqual(SwordStrikeKind.StaffChop, SwordStrikes.StaffOpenerFor(4.0f));
            Assert.IsTrue(SwordStrikes.IsRunningAttack(SwordStrikeKind.StaffRun));
            Assert.AreEqual(0.0f, SwordStrikes.StepMetres(SwordStrikeKind.StaffRun));
        }

        [Test]
        public void TheThrustGoesStraightOutAndKeepsItsLine()
        {
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(SwordStrikeKind.StaffThrust);
            float held = (timing.WindUp + timing.Hold) / timing.Total, contact = timing.Contact / timing.Total;
            SwordFrame from = SwordStrikes.Evaluate(SwordStrikeKind.StaffThrust, held), at = SwordStrikes.Evaluate(SwordStrikeKind.StaffThrust, contact);
            Assert.AreEqual(1.0f, at.HoldsItsLine, "the thrust turns with the clip's shoulders: its long end would swing sideways");
            Assert.Greater(at.Blade.z, 0.95f, "at the contact the pole does not point straight ahead");
            Vector3 travel = FarEnd(at) - FarEnd(from);
            Assert.Greater(travel.z + SwordStrikes.StepMetres(SwordStrikeKind.StaffThrust), 0.6f, "the far end hardly went out, with the lunge's stride");
            Assert.Less(Mathf.Abs(travel.x), 0.25f, "the far end went out sideways, not straight");
        }

        [Test]
        public void ThePolesFarEndStaysOffTheGroundAndTheSweepPassesAtTheShins()
        {
            foreach (SwordStrikeKind kind in Staff)
            {
                for (float t = 0.0f; t <= 1.0f; t += 0.02f)
                {
                    float height = FarEnd(SwordStrikes.Evaluate(kind, t)).y;
                    Assert.Greater(height, -0.1f, $"{kind} at {t:0.00}: the pole's far end is {-height:0.00} m into the ground");
                }
            }
            SwordStrikes.StrikeTiming sweep = SwordStrikes.TimingOf(SwordStrikeKind.StaffSweep);
            float low = FarEnd(SwordStrikes.Evaluate(SwordStrikeKind.StaffSweep, sweep.Contact / sweep.Total)).y;
            Assert.That(low, Is.InRange(0.0f, 0.8f), $"the low sweep lands at {low:0.00} m: not at the shins");
            SwordStrikes.StrikeTiming chop = SwordStrikes.TimingOf(SwordStrikeKind.StaffChop);
            Assert.Greater(FarEnd(SwordStrikes.Evaluate(SwordStrikeKind.StaffChop, chop.Contact / chop.Total)).y, 0.9f, "the chop lands low, not at the body");
        }

        [Test]
        public void TheRisingBlowGoesUpAndTheSpinGoesRoundFromRightToLeft()
        {
            SwordStrikes.StrikeTiming rising = SwordStrikes.TimingOf(SwordStrikeKind.StaffRising);
            float risingHeld = (rising.WindUp + rising.Hold) / rising.Total, risingDone = (rising.Contact + rising.Follow) / rising.Total;
            Assert.Greater(FarEnd(SwordStrikes.Evaluate(SwordStrikeKind.StaffRising, risingDone)).y,
                FarEnd(SwordStrikes.Evaluate(SwordStrikeKind.StaffRising, risingHeld)).y + 1.0f, "the rising blow does not go up");
            SwordStrikes.StrikeTiming spin = SwordStrikes.TimingOf(SwordStrikeKind.StaffSpin);
            SwordFrame wound = SwordStrikes.Evaluate(SwordStrikeKind.StaffSpin, (spin.WindUp + spin.Hold) / spin.Total);
            SwordFrame through = SwordStrikes.Evaluate(SwordStrikeKind.StaffSpin, (spin.Contact + spin.Follow) / spin.Total);
            Assert.Greater(wound.TwistDegrees, 60.0f, "the spin is not wound round to the right");
            Assert.Less(through.TwistDegrees, -35.0f, "the spin does not carry round to the left");
            Assert.Greater(FarEnd(wound).x, 0.8f, "wound up, the pole is not out on the right");
            Assert.Less(FarEnd(through).x, -0.8f, "carried through, the pole is not round on the left");
        }

        [Test]
        public void TheBuiltStaffIsAPlainMetalPoleTwoMetresAndMoreHeldNearOneEnd()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(WeaponsEditor.BoStaffBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "no BoStaff.prefab -- rebuild the scene");
            var staff = prefab.GetComponent<SwordWeapon>();
            Assert.IsTrue(staff.IsStaff);
            Assert.IsFalse(staff.IsOneHanded || staff.IsSpear || staff.Knife);
            Assert.AreEqual(ItemRegistry.BoStaff, staff.ItemId);
            Assert.AreEqual(StaffRules.Tip, staff.BladeLength, 1e-4f);
            float length = StaffRules.Tip - StaffRules.Butt;
            Assert.That(length, Is.InRange(2.0f, 2.5f), "about the length of the spike sword and of the clip's staff");
            float beyondTheLeftHand = -SwordWeapon.DefaultSupportGripDown - StaffRules.Butt;
            Assert.That(beyondTheLeftHand, Is.InRange(0.05f, 0.25f), "the pole is not held near its end: its short end would swing through the body");
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.BoStaff, out ItemDefinition definition));
            Assert.AreEqual(EquipSlots.Primary, definition.EquipSlot);
#endif
        }
    }
}
