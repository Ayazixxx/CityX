using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What a controller's buttons mean (docs/CONTROLLER.md; owner, 2026-11-25: "make it playable on controller"): a shoulder held makes the
    /// face buttons the hotbar, B tapped dashes and held crouches, X held reloads, L3's sprint stays on until the stick is let go, and the
    /// sticks' rest is taken out.
    /// </summary>
    public sealed class PadLayoutTests
    {
        private const float Dt = 1.0f / 60.0f;

        private static PadLayout.Buttons Face(int face, bool lb = false, bool rb = false) => new PadLayout.Buttons
        {
            South = face == PadLayout.South, East = face == PadLayout.East, West = face == PadLayout.West, North = face == PadLayout.North,
            LeftShoulder = lb, RightShoulder = rb,
        };

        [Test]
        public void AShoulderHeldMakesTheFaceButtonsTheHotbar()
        {
            Assert.AreEqual(0, PadLayout.SlotOf(PadLayout.South, true, false), "LB + A is not slot 1");
            Assert.AreEqual(3, PadLayout.SlotOf(PadLayout.North, true, false), "LB + Y is not slot 4");
            Assert.AreEqual(4, PadLayout.SlotOf(PadLayout.South, false, true), "RB + A is not slot 5");
            Assert.AreEqual(7, PadLayout.SlotOf(PadLayout.North, false, true), "RB + Y is not slot 8");
            Assert.AreEqual(8, PadLayout.SlotOf(PadLayout.South, true, true), "both + A is not slot 9");
            Assert.AreEqual(9, PadLayout.SlotOf(PadLayout.East, true, true), "both + B is not slot 0");
            Assert.AreEqual(-1, PadLayout.SlotOf(PadLayout.West, true, true));
            Assert.AreEqual(-1, PadLayout.SlotOf(PadLayout.South, false, false));

            var pad = new PadLayout();
            pad.Step(Face(-1, lb: true), Dt);
            PadLayout.Frame cast = pad.Step(Face(PadLayout.East, lb: true), Dt);
            Assert.AreEqual(1, cast.SlotPressed, "LB held, B did not press slot 2");
            Assert.IsFalse(cast.Dash.Pressed || cast.Jump.Pressed, "a hotbar press also dashed or jumped");
            // The shoulder let go before the face button: the button is still the slot's until it comes up (a held spell keeps charging).
            PadLayout.Frame still = pad.Step(Face(PadLayout.East), Dt);
            Assert.AreEqual(1, still.SlotHeld);
            PadLayout.Frame up = pad.Step(Face(-1), Dt);
            Assert.AreEqual(1, up.SlotReleased);
            Assert.IsFalse(up.Dash.Pressed, "letting go of a hotbar button dashed");
        }

        [Test]
        public void AJumpsAndBTappedDashesButHeldCrouches()
        {
            var pad = new PadLayout();
            Assert.IsTrue(pad.Step(Face(PadLayout.South), Dt).Jump.Pressed, "A did not jump");
            Assert.IsTrue(pad.Step(Face(PadLayout.South), Dt).Jump.Held);
            Assert.IsTrue(pad.Step(Face(-1), Dt).Jump.Released);

            PadLayout.Frame tapDown = pad.Step(Face(PadLayout.East), Dt);
            Assert.IsFalse(tapDown.Dash.Pressed, "the dash went before B was known to be a tap");
            Assert.IsTrue(pad.Step(Face(-1), Dt).Dash.Pressed, "B tapped did not dash");

            bool crouched = false, dashed = false;
            for (int i = 0; i < 60; i++)
            {
                PadLayout.Frame frame = pad.Step(Face(PadLayout.East), Dt);
                crouched |= frame.Crouch.Held;
                dashed |= frame.Dash.Pressed;
            }
            PadLayout.Frame let = pad.Step(Face(-1), Dt);
            Assert.IsTrue(crouched, "B held did not crouch");
            Assert.IsFalse(dashed || let.Dash.Pressed, "B held dashed as well");
            Assert.IsTrue(let.Crouch.Released, "letting go of B did not stand up");
        }

        [Test]
        public void XIsInteractAndHeldItReloads()
        {
            var pad = new PadLayout();
            Assert.IsTrue(pad.Step(Face(PadLayout.West), Dt).Interact.Pressed, "X did not interact");
            int reloads = 0;
            for (int i = 0; i < 60; i++)
            {
                if (pad.Step(Face(PadLayout.West), Dt).Reload.Pressed) reloads++;
            }
            Assert.AreEqual(1, reloads, "X held did not reload once");
            Assert.IsTrue(pad.Step(Face(-1), Dt).Interact.Released);
        }

        [Test]
        public void TheStickPressedSprintsUntilTheStickIsLetGo()
        {
            var pad = new PadLayout();
            Assert.IsTrue(pad.Step(new PadLayout.Buttons { LeftStickPress = true, Moving = true }, Dt).Sprint.Pressed, "L3 did not sprint");
            Assert.IsTrue(pad.Step(new PadLayout.Buttons { Moving = true }, Dt).Sprint.Held, "the sprint did not stay on with L3 let go");
            Assert.IsTrue(pad.Step(new PadLayout.Buttons { Moving = false }, Dt).Sprint.Released, "the stick let go, the sprint went on");
            Assert.IsFalse(pad.Step(new PadLayout.Buttons { Moving = true }, Dt).Sprint.Held, "moving again sprinted without L3");
        }

        [Test]
        public void TheSticksRestIsTakenOut()
        {
            Assert.AreEqual(Vector2.zero, PadLayout.Stick(new Vector2(0.1f, 0.1f)), "a stick at rest moved the body");
            Assert.AreEqual(1.0f, PadLayout.Stick(new Vector2(0.0f, 1.0f)).magnitude, 1e-4f, "a stick pushed all the way is not full");
            Assert.Less(PadLayout.Stick(new Vector2(0.0f, 0.3f)).magnitude, 0.2f, "just past the rest is not a small push");
            Assert.Less(PadLayout.Look(new Vector2(0.5f, 0.0f)).x, PadLayout.Stick(new Vector2(0.5f, 0.0f)).x, "the camera stick is not finer near the middle");
        }

        [Test]
        public void YSwapsTheWeapon()
        {
            var pad = new PadLayout();
            Assert.IsTrue(pad.Step(Face(PadLayout.North), Dt).SwapWeapon);
            Assert.IsFalse(pad.Step(Face(PadLayout.North), Dt).SwapWeapon, "Y held swapped again");
        }
    }
}
