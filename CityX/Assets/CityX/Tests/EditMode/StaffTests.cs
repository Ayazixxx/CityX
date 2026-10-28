using CityX.Weapons;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The staffs' click as time (StaffCharge, docs/STAFFS.md): a press starts a charge, a release sends the shot 8 frames later with the charge
    /// held, the next cannot start until the staff's pace allows, a cancel drops it; and the Fireball Staff's charge (up to 1.5 s) and mana.
    /// </summary>
    public sealed class StaffTests
    {
        private const float Frame = 1.0f / 60.0f;

        [Test]
        public void AClickStartsAChargeAndTheShotLeavesEightFramesAfterTheRelease()
        {
            var charge = new StaffCharge(0.5f);
            Assert.AreEqual(StaffCharge.Step.Started, charge.Tick(true, true, Frame));
            Assert.IsTrue(charge.Charging);
            Assert.AreEqual(StaffCharge.Step.None, charge.Tick(false, true, Frame), "let go: the shot is on its way");
            Assert.IsTrue(charge.Releasing);
            int frames = 0;
            StaffCharge.Step step;
            do
            {
                step = charge.Tick(false, true, Frame);
                frames++;
            }
            while (step != StaffCharge.Step.Fire && frames < 60);
            Assert.AreEqual(StaffCharge.Step.Fire, step);
            Assert.AreEqual(8, frames, 1, "Shadow Flare's surge comes 8 frames after the release");
            Assert.AreEqual(0.0f, charge.ReleasedSeconds, 1e-4f, "a click carries no charge");
            Assert.IsTrue(charge.Busy, "the staff is still out after the shot");
        }

        [Test]
        public void HoldingChargesAndTheShotCarriesHowLongItWasHeld()
        {
            var charge = new StaffCharge(0.5f);
            charge.Tick(true, true, Frame);
            for (int i = 0; i < 30; i++)
            {
                charge.Tick(true, true, Frame);
            }
            charge.Tick(false, true, Frame);
            Assert.AreEqual(0.5f, charge.ReleasedSeconds, 1e-3f);
        }

        [Test]
        public void TheNextChargeWaitsForTheStaffsPaceAndForTheCasterToAllowIt()
        {
            var charge = new StaffCharge(0.5f);
            charge.Tick(true, true, Frame);
            charge.Tick(false, true, Frame);
            for (int i = 0; i < 10; i++)
            {
                charge.Tick(false, true, Frame);   // the shot leaves
            }
            Assert.AreEqual(StaffCharge.Step.None, charge.Tick(true, true, Frame), "too soon after the last");
            for (int i = 0; i < 30; i++)
            {
                charge.Tick(false, true, Frame);
            }
            Assert.AreEqual(StaffCharge.Step.None, charge.Tick(true, false, Frame), "not allowed (no mana)");
            Assert.AreEqual(StaffCharge.Step.Started, charge.Tick(true, true, Frame));
        }

        [Test]
        public void ACancelDropsTheChargeWithoutAShot()
        {
            var charge = new StaffCharge(0.5f);
            charge.Tick(true, true, Frame);
            Assert.IsTrue(charge.Cancel());
            Assert.IsFalse(charge.Charging);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreNotEqual(StaffCharge.Step.Fire, charge.Tick(false, true, Frame));
            }
            Assert.IsFalse(charge.Cancel(), "nothing left to drop");
        }

        [Test]
        public void TheFireballStaffChargesForASecondAndAHalfFromTenManaToThirty()
        {
            Assert.AreEqual(0.0f, FireballCaster.StaffChargeFraction(0.0f), 1e-5f);
            Assert.AreEqual(0.5f, FireballCaster.StaffChargeFraction(0.75f), 1e-5f);
            Assert.AreEqual(1.0f, FireballCaster.StaffChargeFraction(5.0f), 1e-5f, "a reasonable maximum: no more after 1.5 s");
            Assert.AreEqual(10.0f, FireballCaster.StaffManaFor(0.0f), 1e-5f);
            Assert.AreEqual(30.0f, FireballCaster.StaffManaFor(1.0f), 1e-5f);
            Assert.Less(FireballCaster.StaffAffordableFraction(9.0f), 0.0f, "not even a plain shot");
            Assert.AreEqual(0.5f, FireballCaster.StaffAffordableFraction(20.0f), 1e-5f);
            Assert.AreEqual(1.0f, FireballCaster.StaffAffordableFraction(100.0f), 1e-5f);
        }
    }
}
