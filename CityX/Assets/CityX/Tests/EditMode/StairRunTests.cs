using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The body running a stair, from how fast its feet rise (StairRun; owner, 2026-11-19: "i accidentally vault when running up stairs
    /// sometimes"): a sprint or a walk up a stair is one, a kerb, a street's hill or a jump onto a crate is not; and how far it has gone since.
    /// </summary>
    public sealed class StairRunTests
    {
        private const float Frame = 1.0f / 60.0f;
        private const float SprintAlong = 7.5f * Frame;

        /// <summary>The body goes <paramref name="frames"/> frames rising <paramref name="risePerFrame"/> and going <paramref name="along"/> a frame; returns its height after.</summary>
        private static float Go(StairRun run, float height, int frames, float risePerFrame, float along, bool onGround = true)
        {
            for (int i = 0; i < frames; i++)
            {
                height += risePerFrame;
                run.Step(height, along, onGround, false, Frame);
            }
            return height;
        }

        [Test]
        public void SprintingOrWalkingUpAStairIsOneAndTheWayOffItIsCounted([Values(7.5f, 4.0f)] float speed)
        {
            var run = new StairRun();
            float along = speed * Frame;
            float height = Go(run, 0.0f, 30, 0.0f, along);
            Assert.IsFalse(run.OnStair, "the flat counted as a stair");
            height = Go(run, height, 30, 0.63f * along, along);   // up a stair of the city's: 0.63 m a metre
            Assert.IsTrue(run.OnStair, $"running up a stair at {speed} m/s was not one");
            Go(run, height, 30, 0.0f, along);
            Assert.IsFalse(run.OnStair, "still on the stair half a second onto the landing");
            Assert.Greater(run.MetresOff, 10 * along, "the way off the stair is not counted");
            float off = run.MetresOff;
            Go(run, height, 30, 0.0f, 0.0f);   // pressed against a wall
            Assert.AreEqual(off, run.MetresOff, 1e-4f, "standing against a wall counted as going on");
        }

        [Test]
        public void AKerbAHillOrAJumpOntoACrateIsNotAStair()
        {
            var kerb = new StairRun();
            float height = Go(kerb, 0.0f, 20, 0.0f, SprintAlong);
            height = Go(kerb, height, 1, 0.15f, SprintAlong);
            Go(kerb, height, 40, 0.0f, SprintAlong);
            Assert.IsTrue(float.IsPositiveInfinity(kerb.MetresOff), "a kerb counted as a stair");

            var hill = new StairRun();
            for (int i = 0; i < 120; i++)
            {
                Go(hill, i * 0.2f * SprintAlong, 1, 0.0f, SprintAlong);   // a 20% street at a sprint
                Assert.IsFalse(hill.OnStair, "a steep street counted as a stair");
            }

            var crate = new StairRun();
            height = Go(crate, 0.0f, 20, 0.0f, SprintAlong);
            height = Go(crate, height, 15, 0.04f, SprintAlong, onGround: false);   // a hop up onto a 0.6 m crate
            Go(crate, height, 30, 0.0f, SprintAlong);
            Assert.IsTrue(float.IsPositiveInfinity(crate.MetresOff), "a jump onto a crate counted as a stair");
        }

        [Test]
        public void TheWalksOwnStairSenseCountsToo()
        {
            var run = new StairRun();
            run.Step(0.0f, SprintAlong, true, true, Frame);
            Assert.IsTrue(run.OnStair);
            Assert.AreEqual(0.0f, run.MetresOff);
        }
    }
}
