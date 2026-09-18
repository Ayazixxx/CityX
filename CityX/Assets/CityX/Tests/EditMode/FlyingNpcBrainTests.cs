using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The flyer's wander loop, stepped rather than watched -- same reasoning
    /// CivilianBrainTests already established: a heading that never changes
    /// or a bounds check that never fires is a bug measured in minutes of
    /// watching the sky, not milliseconds of stepping.
    /// </summary>
    public sealed class FlyingNpcBrainTests
    {
        private const float Step = 1.0f / 60.0f;

        [Test]
        public void TheHeadingIsAlwaysFlat()
        {
            var brain = new FlyingNpcBrain(1, Vector3.zero);
            Vector3 position = new Vector3(0.0f, 40.0f, 0.0f);

            for (int i = 0; i < 4000; i++)
            {
                Vector3 heading = brain.Tick(position, Step);
                Assert.AreEqual(0.0f, heading.y, 0.0001f, "the wander tried to climb or dive on its own -- that is FlyingNpcSteering's job, not the brain's");
                position += heading * (9.0f * Step);
            }
        }

        [Test]
        public void TheHeadingIsAlwaysAUnitVector()
        {
            var brain = new FlyingNpcBrain(2, Vector3.zero);
            Vector3 position = Vector3.zero;

            for (int i = 0; i < 2000; i++)
            {
                Vector3 heading = brain.Tick(position, Step);
                Assert.AreEqual(1.0f, heading.magnitude, 0.001f);
                position += heading * (9.0f * Step);
            }
        }

        /// <summary>
        /// Two flyers created on the same frame must not turn in unison --
        /// same "a crowd moving as one body reads as a formation" reasoning
        /// CivilianBrainTests already pins for pedestrians.
        /// </summary>
        [Test]
        public void TwoFlyersDoNotTurnInLockstep()
        {
            var first = new FlyingNpcBrain(11, Vector3.zero);
            var second = new FlyingNpcBrain(12, Vector3.zero);

            Assert.AreNotEqual(first.TimeRemaining, second.TimeRemaining,
                "two flyers start their first heading change at exactly the same moment");
        }

        [Test]
        public void TheHeadingEventuallyChanges()
        {
            var brain = new FlyingNpcBrain(3, Vector3.zero);
            Vector3 position = Vector3.zero;
            Vector3 startHeading = brain.Heading;

            bool changed = false;
            for (int i = 0; i < Mathf.CeilToInt((FlyingNpcBrain.WanderMaxSeconds + 1.0f) / Step); i++)
            {
                Vector3 heading = brain.Tick(position, Step);
                position += heading * (9.0f * Step);
                if (Vector3.Distance(heading, startHeading) > 0.01f)
                {
                    changed = true;
                    break;
                }
            }
            Assert.IsTrue(changed, "the flyer held the exact same heading for longer than WanderMaxSeconds");
        }

        /// <summary>
        /// Past WorldBoundsRadius the wander stops entirely and it heads
        /// straight back -- not just "eventually", every single tick, since
        /// this is the one thing standing between a flyer and drifting off
        /// into unbuilt terrain forever.
        /// </summary>
        [Test]
        public void PastTheBoundsItHeadsStraightBackToCentre()
        {
            var brain = new FlyingNpcBrain(4, Vector3.zero);
            Vector3 farAway = new Vector3(FlyingNpcBrain.WorldBoundsRadius + 50.0f, 0.0f, 0.0f);

            Vector3 heading = brain.Tick(farAway, Step);

            Assert.Less(heading.x, -0.9f, "far out on the +X side, the flyer should head back toward -X");
        }

        [Test]
        public void WellInsideTheBoundsIsNeverForcedBack()
        {
            var brain = new FlyingNpcBrain(5, Vector3.zero);
            Vector3 nearCentre = new Vector3(10.0f, 0.0f, 0.0f);

            // Just confirms this does not throw or misbehave near the centre
            // -- the actual heading is whatever the wander picked, which is
            // random by design and not itself worth asserting on here.
            Vector3 heading = brain.Tick(nearCentre, Step);
            Assert.AreEqual(1.0f, heading.magnitude, 0.001f);
        }

        [Test]
        public void TheSameSeedWandersTheSameRoute()
        {
            Vector3 Simulate(int seed)
            {
                var brain = new FlyingNpcBrain(seed, Vector3.zero);
                Vector3 position = Vector3.zero;
                for (int i = 0; i < Mathf.CeilToInt(60.0f / Step); i++)
                {
                    position += brain.Tick(position, Step) * (9.0f * Step);
                }
                return position;
            }

            Vector3 first = Simulate(42);
            Vector3 second = Simulate(42);

            Assert.AreEqual(first.x, second.x, 0.01f);
            Assert.AreEqual(first.z, second.z, 0.01f);
        }
    }
}
