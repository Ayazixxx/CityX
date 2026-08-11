using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The pedestrian's wander loop, stepped rather than watched.
    ///
    /// Every failure mode here is measured in minutes of standing and staring:
    /// a civilian that never sets off, one that never arrives, one that gets
    /// stuck in a walk it cannot end. Stepping the state machine turns each of
    /// those into a fraction of a second.
    /// </summary>
    public class CivilianBrainTests
    {
        private const float Step = 1.0f / 60.0f;

        /// <summary>
        /// Walks the body wherever the brain says, at walking pace, the way the
        /// real component does. Returns where it ended up.
        /// </summary>
        private static Vector3 Simulate(CivilianBrain brain, Vector3 start, float seconds)
        {
            Vector3 position = start;
            int steps = Mathf.CeilToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                Vector3 direction = brain.Tick(position, Step);
                position += direction * (CivilianBrain.WalkSpeed * Step);
            }
            return position;
        }

        [Test]
        public void APedestrianEventuallySetsOff()
        {
            var brain = new CivilianBrain(1);
            Vector3 start = new Vector3(12.0f, 0.0f, 12.0f);

            Vector3 end = Simulate(brain, start, 12.0f);

            Assert.Greater(Vector3.Distance(start, end), 1.0f,
                "twelve seconds in and the pedestrian has not moved at all");
        }

        [Test]
        public void APedestrianActuallyArrivesRatherThanOrbiting()
        {
            var brain = new CivilianBrain(2);
            Vector3 position = new Vector3(12.0f, 0.0f, 12.0f);

            // Run until it commits to a walk, then follow that walk to its end.
            for (int i = 0; i < 2000 && !brain.IsWalking; i++)
            {
                position += brain.Tick(position, Step) * (CivilianBrain.WalkSpeed * Step);
            }
            Assert.IsTrue(brain.IsWalking, "the pedestrian never started walking");

            Vector3 target = brain.Target;
            float startedWith = brain.TimeRemaining;

            for (int i = 0; i < 4000 && brain.IsWalking; i++)
            {
                position += brain.Tick(position, Step) * (CivilianBrain.WalkSpeed * Step);
            }

            Assert.IsFalse(brain.IsWalking, "the walk never ended");
            Assert.Less(Vector3.Distance(
                    new Vector3(position.x, 0.0f, position.z),
                    new Vector3(target.x, 0.0f, target.z)),
                CivilianBrain.ArriveDistance + 0.1f,
                "the walk ended without reaching the target -- it timed out instead");
            Assert.Greater(startedWith, 0.0f);
        }

        /// <summary>
        /// A pedestrian pinned in place -- shoved into a corner, standing on
        /// something -- must still give up and choose again rather than leaning
        /// on it for the rest of the session.
        /// </summary>
        [Test]
        public void AWalkThatCannotProgressGivesUpAndPicksAgain()
        {
            var brain = new CivilianBrain(3);
            var stuck = new Vector3(12.0f, 0.0f, 12.0f);

            // Never move, whatever it asks for.
            for (int i = 0; i < 2000 && !brain.IsWalking; i++)
            {
                brain.Tick(stuck, Step);
            }
            Assert.IsTrue(brain.IsWalking);

            int steps = Mathf.CeilToInt((CivilianBrain.WalkTimeoutSeconds + 1.0f) / Step);
            for (int i = 0; i < steps; i++)
            {
                brain.Tick(stuck, Step);
            }

            Assert.IsFalse(brain.IsWalking,
                "a pedestrian that made no progress at all never gave up on its target");
        }

        /// <summary>
        /// Two pedestrians spawned on the same frame must not step off in
        /// unison. A crowd moving as one body reads as a formation, not a
        /// street.
        /// </summary>
        [Test]
        public void ACrowdDoesNotMoveInLockstep()
        {
            var first = new CivilianBrain(11);
            var second = new CivilianBrain(12);

            Assert.AreNotEqual(first.TimeRemaining, second.TimeRemaining,
                "two pedestrians start their first idle at exactly the same moment");
        }

        [Test]
        public void TheSameSeedWalksTheSameRoute()
        {
            Vector3 start = new Vector3(12.0f, 0.0f, 12.0f);

            Vector3 first = Simulate(new CivilianBrain(42), start, 30.0f);
            Vector3 second = Simulate(new CivilianBrain(42), start, 30.0f);

            Assert.AreEqual(first.x, second.x, 0.001f);
            Assert.AreEqual(first.z, second.z, 0.001f);
        }

        [Test]
        public void DifferentSeedsWalkDifferentRoutes()
        {
            Vector3 start = new Vector3(12.0f, 0.0f, 12.0f);

            Vector3 first = Simulate(new CivilianBrain(1), start, 40.0f);
            Vector3 second = Simulate(new CivilianBrain(2), start, 40.0f);

            Assert.Greater(Vector3.Distance(first, second), 0.5f,
                "two differently seeded pedestrians ended up in the same place");
        }

        /// <summary>
        /// The direction must stay flat. A pedestrian aiming at a target on a
        /// terrace above them would walk into the retaining wall, and one
        /// aiming below would try to walk into the ground.
        /// </summary>
        [Test]
        public void TheWalkDirectionIsAlwaysFlat()
        {
            var brain = new CivilianBrain(5);
            var position = new Vector3(12.0f, 40.0f, 12.0f);

            for (int i = 0; i < 4000; i++)
            {
                Vector3 direction = brain.Tick(position, Step);
                Assert.AreEqual(0.0f, direction.y, 0.0001f, "the pedestrian tried to walk uphill");
                position += direction * (CivilianBrain.WalkSpeed * Step);
                position.y = 40.0f;
            }
        }

        /// <summary>
        /// Over a long wander the pedestrian should get somewhere, not shuffle
        /// back and forth around one spot. Ten minutes at 1.5 m/s could cover
        /// 900m in a straight line; a wander will not, but it should manage
        /// more than a few metres.
        /// </summary>
        [Test]
        public void ALongWanderActuallyGoesSomewhere()
        {
            var brain = new CivilianBrain(77);
            var start = new Vector3(12.0f, 0.0f, 12.0f);

            Vector3 end = Simulate(brain, start, 600.0f);

            Assert.Greater(Vector3.Distance(start, end), 10.0f,
                "ten minutes of walking and the pedestrian is still on the same corner");
        }
    }
}
