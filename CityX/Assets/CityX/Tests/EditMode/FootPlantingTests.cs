using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The foot placement's rules on paper (Character.FootPlanting): the hips drop for the foot that has to reach down, a planted foot is held where it
    /// landed, a lifted or outrun foot is let go gently, a landing dips and recovers, and nothing happens in the air.
    /// </summary>
    public sealed class FootPlantingTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const float Sole = 0.135f;
        private const float Floor = 0.0f;

        private static FootPlanting.Sample Foot(float x, float z, float lift, float groundY, bool hasGround = true)
        {
            return new FootPlanting.Sample
            {
                Animated = new Vector3(x, Floor + Sole + lift, z),
                HasGround = hasGround,
                Ground = new Vector3(x, groundY, z),
                Normal = Vector3.up,
            };
        }

        private static void Run(FootPlanting feet, int frames, System.Func<int, FootPlanting.Sample> left, System.Func<int, FootPlanting.Sample> right,
            bool active = true, bool releaseLocks = false)
        {
            for (int i = 0; i < frames; i++)
            {
                feet.Step(Dt, active, left(i), right(i), Floor, Sole, releaseLocks);
            }
        }

        [Test]
        public void AFootOverLowerGroundDropsTheHipsByTheDifferenceAndHigherGroundDoesNot()
        {
            var feet = new FootPlanting();
            // The left foot hangs over a step 20 cm down; the right stands on the floor.
            Run(feet, 60, i => Foot(-0.12f, 0.0f, 0.0f, -0.2f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.AreEqual(0.2f, feet.HipDrop, 0.01f, "the hips did not drop for the low foot");
            Assert.AreEqual(-0.2f + Sole + FootPlanting.SoleLift, feet.Of(0).Target.y, 0.005f, "the low foot's sole is not on its step");
            Assert.AreEqual(Sole + FootPlanting.SoleLift, feet.Of(1).Target.y, 0.005f, "the other foot left the floor");
            Assert.AreEqual(1.0f, feet.Of(0).Weight, 0.001f);

            // Now the right foot is over a 15 cm kerb and the left on the floor: the knee bends, the hips stay.
            Run(feet, 90, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.15f));
            Assert.Less(feet.HipDrop, 0.01f, "higher ground should bend the knee, not drop the hips");
            Assert.AreEqual(0.15f + Sole + FootPlanting.SoleLift, feet.Of(1).Target.y, 0.005f, "the foot is not up on the kerb");
        }

        [Test]
        public void TheDropNeverExceedsWhatTheLegsCanGive()
        {
            var feet = new FootPlanting();
            Run(feet, 60, i => Foot(-0.12f, 0.0f, 0.0f, -0.45f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.LessOrEqual(feet.HipDrop, FootPlanting.MaxHipDrop + 1e-4f);
            // Over a real drop (a cliff edge) the foot is simply not placed, and the hips stay up.
            var edge = new FootPlanting();
            Run(edge, 60, i => Foot(-0.12f, 0.0f, 0.0f, -3.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.Less(edge.HipDrop, 0.01f, "a foot over the void dragged the hips down");
            Assert.Less(edge.Of(0).Weight, 0.01f, "a foot over the void was still placed");
            Assert.AreEqual(1.0f, edge.Of(1).Weight, 0.001f, "the foot on the floor was not placed");
        }

        [Test]
        public void APlantedFootIsHeldWhereItLandedWhileTheClipSlidesIt()
        {
            var feet = new FootPlanting();
            // The left foot lands at z = 0 and the clip then slides it back a centimetre a frame (the body is faster than the clip).
            Run(feet, 1, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.3f, 0.1f, 0.0f));
            Assert.IsTrue(feet.IsLocked(0), "a foot on the floor was not planted");
            for (int i = 1; i <= 15; i++)
            {
                feet.Step(Dt, true, Foot(-0.12f, -0.01f * i, 0.0f, 0.0f), Foot(0.12f, 0.3f, 0.1f, 0.0f), Floor, Sole);
                Assert.IsTrue(feet.IsLocked(0), $"the planted foot was let go at frame {i}");
                Assert.AreEqual(0.0f, feet.Of(0).Target.z, 0.001f, $"the planted foot slid at frame {i}");
            }
            Assert.IsFalse(feet.IsLocked(1), "a foot in the air was planted");
        }

        [Test]
        public void LiftingTheFootLetsItGoAndItFadesBackToTheClip()
        {
            var feet = new FootPlanting();
            Run(feet, 5, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.IsTrue(feet.IsLocked(0));
            // The clip lifts the left foot into its swing and carries it forward.
            feet.Step(Dt, true, Foot(-0.12f, 0.2f, 0.12f, 0.0f), Foot(0.12f, 0.0f, 0.0f, 0.0f), Floor, Sole);
            Assert.IsFalse(feet.IsLocked(0), "a lifted foot stayed planted");
            Assert.Less(feet.Of(0).Target.z, 0.2f, "the let-go foot jumped straight to the clip instead of fading");
            Run(feet, Mathf.CeilToInt(FootPlanting.LockFadeSeconds / Dt) + 2, i => Foot(-0.12f, 0.2f, 0.12f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.AreEqual(0.2f, feet.Of(0).Target.z, 0.002f, "the let-go foot never caught up with the clip");
            Assert.AreEqual(Sole + 0.12f + FootPlanting.SoleLift, feet.Of(0).Target.y, 0.005f, "a swinging foot lost the clip's own lift");
        }

        [Test]
        public void AFootTheClipSwingsForwardIsLetGoHoweverLowItStays()
        {
            // A shuffling walk (a sword guard's) never lifts its feet much; the swing shows as the foot moving AHEAD of its lock, the way the body goes.
            var feet = new FootPlanting();
            Vector3 forward = Vector3.forward * 2.0f;
            feet.Step(Dt, true, Foot(-0.12f, 0.0f, 0.0f, 0.0f), Foot(0.12f, 0.0f, 0.0f, 0.0f), Floor, Sole, false, forward);
            Assert.IsTrue(feet.IsLocked(0));
            // The clip drags it back a little (planted) ...
            feet.Step(Dt, true, Foot(-0.12f, -0.03f, 0.01f, 0.0f), Foot(0.12f, 0.0f, 0.0f, 0.0f), Floor, Sole, false, forward);
            Assert.IsTrue(feet.IsLocked(0), "a foot a little behind its lock is still planted");
            // ... then swings it forward, two centimetres up at most.
            feet.Step(Dt, true, Foot(-0.12f, 0.12f, 0.02f, 0.0f), Foot(0.12f, 0.0f, 0.0f, 0.0f), Floor, Sole, false, forward);
            Assert.IsFalse(feet.IsLocked(0), "a foot swung forward of its lock was still held, dragging it");
        }

        [Test]
        public void AFootPulledTooFarFromTheClipIsLetGo()
        {
            var feet = new FootPlanting();
            Run(feet, 3, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.IsTrue(feet.IsLocked(0));
            feet.Step(Dt, true, Foot(-0.12f, -(FootPlanting.MaxLockStretch + 0.05f), 0.0f, 0.0f), Foot(0.12f, 0.0f, 0.0f, 0.0f), Floor, Sole);
            Assert.IsFalse(feet.IsLocked(0), "a foot the body had outrun was still held");
        }

        [Test]
        public void TurningOnTheSpotReleasesTheLocks()
        {
            var feet = new FootPlanting();
            Run(feet, 3, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.IsTrue(feet.IsLocked(0) && feet.IsLocked(1));
            Run(feet, 1, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f), releaseLocks: true);
            Assert.IsFalse(feet.IsLocked(0) || feet.IsLocked(1), "feet held in the world while the body spins would twist the legs");
        }

        [Test]
        public void ALandingDipsTheHipsAndRecovers()
        {
            var feet = new FootPlanting();
            feet.Land(8.0f);
            float deepest = 0.0f;
            for (int i = 0; i < 18; i++)
            {
                feet.Step(Dt, true, Foot(-0.12f, 0.0f, 0.0f, 0.0f), Foot(0.12f, 0.0f, 0.0f, 0.0f), Floor, Sole);
                deepest = Mathf.Max(deepest, feet.LandingDip);
            }
            Assert.Greater(deepest, 0.06f, "an 8 m/s landing should visibly bend the knees");
            Assert.LessOrEqual(deepest, FootPlanting.MaxLandingDip + 1e-4f);
            Run(feet, 90, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.Less(feet.LandingDip, 0.005f, "the landing dip never recovered");
            // A step down is not a landing.
            var step = new FootPlanting();
            step.Land(1.0f);
            Run(step, 10, i => Foot(-0.12f, 0.0f, 0.0f, 0.0f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.Less(step.LandingDip, 0.001f, "a small step dipped the hips");
        }

        [Test]
        public void InTheAirEverythingEasesToNothing()
        {
            var feet = new FootPlanting();
            Run(feet, 60, i => Foot(-0.12f, 0.0f, 0.0f, -0.2f), i => Foot(0.12f, 0.0f, 0.0f, 0.0f));
            Assert.Greater(feet.HipDrop, 0.1f);
            Run(feet, 40, i => Foot(-0.12f, 0.0f, 0.5f, -0.2f), i => Foot(0.12f, 0.0f, 0.5f, 0.0f), active: false);
            Assert.Less(feet.HipDrop, 0.01f, "the hips stayed down in the air");
            Assert.IsFalse(feet.IsLocked(0) || feet.IsLocked(1), "a lock survived leaving the ground");
            Assert.Less(feet.Of(0).Weight, 0.001f, "the IK stayed on in the air");
        }
    }
}
