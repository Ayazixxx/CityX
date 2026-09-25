using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The player's noises. What is worth pinning here is not "does a clip
    /// play" -- that needs ears -- but the bookkeeping around it, which is
    /// where the real bugs are: loops that never stop, and an indoor count that
    /// sticks.
    /// </summary>
    public sealed class PlayerAudioTests
    {
        private GameObject _object;
        private PlayerAudio _audio;
        private Transform _outer;
        private Transform _inner;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Player");
            _audio = _object.AddComponent<PlayerAudio>();
            _outer = new GameObject("OuterBuilding").transform;
            _inner = new GameObject("InnerBuilding").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_outer.gameObject);
            Object.DestroyImmediate(_inner.gameObject);
        }

        [Test]
        public void SteppingInsideAndBackOutLeavesYouOutdoors()
        {
            Assert.IsFalse(_audio.IsIndoors);

            _audio.EnterBuilding(_outer);
            Assert.IsTrue(_audio.IsIndoors);
            Assert.AreSame(_outer, _audio.CurrentBuilding);

            _audio.ExitBuilding(_outer);
            Assert.IsFalse(_audio.IsIndoors);
            Assert.IsNull(_audio.CurrentBuilding);
        }

        /// <summary>
        /// Overlapping footprints. Leaving the OUTER of two must leave you
        /// still inside the inner one -- a plain count gets this right by
        /// accident but cannot say which building you are in, and a plain
        /// pop-the-end gets the wrong answer.
        /// </summary>
        [Test]
        public void LeavingTheOuterOfTwoLeavesYouInTheInnerOne()
        {
            _audio.EnterBuilding(_outer);
            _audio.EnterBuilding(_inner);

            _audio.ExitBuilding(_outer);

            Assert.IsTrue(_audio.IsIndoors, "walking out of one building of two put us on the street");
            Assert.AreSame(_inner, _audio.CurrentBuilding);
        }

        /// <summary>
        /// A volume nobody told us we entered -- streamed in around a body
        /// already standing inside it, which this port's chunk loading makes
        /// entirely possible. It still has to come off the stack, or the count
        /// sticks above zero and the street is never audible again.
        /// </summary>
        [Test]
        public void LeavingSomewhereWeWereNeverToldWeEnteredStillGetsUsOut()
        {
            _audio.EnterBuilding(_outer);

            _audio.ExitBuilding(_inner);

            Assert.IsFalse(_audio.IsIndoors,
                "an unrecognised exit left the player permanently indoors");
        }

        [Test]
        public void LeavingWithNothingOnTheStackDoesNotThrow()
        {
            Assert.DoesNotThrow(() => _audio.ExitBuilding(_outer));
            Assert.IsFalse(_audio.IsIndoors);
        }

        /// <summary>
        /// Footsteps need ground under them. A sprint jump carries its momentum
        /// through the air, so gating on "moving" alone leaves you audibly
        /// walking across the sky -- which is Godot's own stated reason for
        /// checking is_on_floor rather than just the speed.
        /// </summary>
        [Test]
        public void NoFootstepsInMidAir()
        {
            Assert.AreEqual(PlayerAudio.Step.None,
                PlayerAudio.StepFor(moving: true, grounded: false, sprinting: false));
            Assert.AreEqual(PlayerAudio.Step.None,
                PlayerAudio.StepFor(moving: true, grounded: false, sprinting: true));
        }

        [Test]
        public void MovingOnTheGroundPicksTheLoopThatMatchesTheGait()
        {
            Assert.AreEqual(PlayerAudio.Step.Walk,
                PlayerAudio.StepFor(moving: true, grounded: true, sprinting: false));
            Assert.AreEqual(PlayerAudio.Step.Run,
                PlayerAudio.StepFor(moving: true, grounded: true, sprinting: true));
        }

        [Test]
        public void StandingStillIsSilent()
        {
            Assert.AreEqual(PlayerAudio.Step.None,
                PlayerAudio.StepFor(moving: false, grounded: true, sprinting: false));

            // And a body that WAS walking goes quiet when it stops, rather than
            // keeping the loop because nothing told it to end.
            _audio.UpdateFootsteps(moving: true, grounded: true, sprinting: false);
            _audio.UpdateFootsteps(moving: false, grounded: true, sprinting: false);
            Assert.AreEqual(PlayerAudio.Step.None, _audio.Stepping);
        }

        /// <summary>
        /// Loops do not run out on their own, so every path that takes the
        /// controls away has to stop them by hand -- otherwise a corpse keeps
        /// walking on the spot, audibly.
        /// </summary>
        [Test]
        public void SilencingStopsTheLoop()
        {
            _audio.UpdateFootsteps(moving: true, grounded: true, sprinting: true);
            Assert.AreEqual(PlayerAudio.Step.Run, _audio.Stepping);

            _audio.SilenceFootsteps();
            Assert.AreEqual(PlayerAudio.Step.None, _audio.Stepping);
        }
    }
}
