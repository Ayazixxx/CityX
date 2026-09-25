using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    public sealed class WeaponAimInputTests
    {
        [Test]
        public void RightPressAloneStartsAimingWithoutActingYet()
        {
            var input = new WeaponAimInput();
            WeaponAimInput.Action action = input.Tick(rightPressedThisFrame: true, rightReleasedThisFrame: false, leftPressedThisFrame: false);

            Assert.AreEqual(WeaponAimInput.Action.None, action);
            Assert.IsTrue(input.IsAiming);
        }

        [Test]
        public void ReleasingRightWithoutEverPressingLeftCancelsForFree()
        {
            var input = new WeaponAimInput();
            input.Tick(true, false, false);

            WeaponAimInput.Action action = input.Tick(false, true, false);

            Assert.AreEqual(WeaponAimInput.Action.CancelAim, action);
            Assert.IsFalse(input.IsAiming);
        }

        [Test]
        public void LeftWhileAimingThrows()
        {
            var input = new WeaponAimInput();
            input.Tick(true, false, false);

            WeaponAimInput.Action action = input.Tick(false, false, true);

            Assert.AreEqual(WeaponAimInput.Action.Throw, action);
            Assert.IsFalse(input.IsAiming, "the throw ends the aim, same as the cancel path does");
        }

        [Test]
        public void LeftWithoutEverAimingIsAMeleeSwing()
        {
            var input = new WeaponAimInput();

            WeaponAimInput.Action action = input.Tick(false, false, true);

            Assert.AreEqual(WeaponAimInput.Action.Melee, action);
        }

        [Test]
        public void ReleasingRightWithNoPriorPressDoesNothing()
        {
            var input = new WeaponAimInput();

            WeaponAimInput.Action action = input.Tick(false, true, false);

            Assert.AreEqual(WeaponAimInput.Action.None, action);
            Assert.IsFalse(input.IsAiming);
        }

        [Test]
        public void ALeftPressBeforeAnyRightPressDoesNotRetroactivelyCountAsAThrow()
        {
            var input = new WeaponAimInput();
            WeaponAimInput.Action first = input.Tick(false, false, true);
            WeaponAimInput.Action second = input.Tick(true, false, false);

            Assert.AreEqual(WeaponAimInput.Action.Melee, first);
            Assert.AreEqual(WeaponAimInput.Action.None, second);
            Assert.IsTrue(input.IsAiming);
        }
    }
}
