using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The foot's path on a stair, as measured from the recorded clips (Character.StairGait, docs/STAIRS.md).</summary>
    public sealed class StairGaitTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void AStepStartsWhereTheFootWasAndEndsWhereItLands(bool up)
        {
            StairGait.Swing(up, 0.0f, out float along, out float height, out float heel);
            Assert.AreEqual(0.0f, along);
            Assert.AreEqual(0.0f, height);
            Assert.AreEqual(0.0f, heel);
            StairGait.Swing(up, 1.0f, out along, out height, out heel);
            Assert.AreEqual(1.0f, along);
            Assert.AreEqual(1.0f, height);
            Assert.AreEqual(StairGait.LandingPitch(up), heel);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AFootNeverGoesBackAlongTheGround(bool up)
        {
            float last = 0.0f;
            for (int i = 0; i <= 100; i++)
            {
                StairGait.Swing(up, i / 100.0f, out float along, out _, out _);
                Assert.GreaterOrEqual(along, last, $"at {i}%");
                last = along;
            }
        }

        [Test]
        public void GoingUpItRisesBeforeItTravelsAndComesDownOntoTheTread()
        {
            StairGait.Swing(true, 0.45f, out float along, out float height, out float heel);
            Assert.Less(along, 0.3f, "it went out before it went up: the riser is in the way");
            Assert.Greater(height, 0.8f);
            Assert.Greater(heel, 30.0f, "the heel stayed down as the foot left");
            StairGait.Swing(true, 0.7f, out _, out height, out _);
            Assert.Greater(height, 1.1f, "it never cleared the tread it lands on");
        }

        [Test]
        public void GoingDownItTravelsBeforeItFalls()
        {
            StairGait.Swing(false, 0.44f, out float along, out float height, out _);
            Assert.Greater(along, 0.85f, "it fell before it reached over the edge");
            Assert.Less(height, 0.5f);
        }

        [TestCase(true, StairGait.WalkPace)]
        [TestCase(false, StairGait.WalkPace)]
        [TestCase(true, StairGait.RunPace)]
        [TestCase(false, StairGait.RunPace)]
        public void AFootsTimeOnItsTreadAndInTheAirMakeOneCycle(bool up, float speed)
        {
            float onTheTread = StairGait.LandAhead(up, speed) + StairGait.LiftBehind(up, speed);
            float inTheAir = speed * StairGait.SwingSeconds(up, speed);
            Assert.AreEqual(StairGait.StrideMetres, onTheTread + inTheAir, 0.03f, "the feet would drift out of step with the body");
        }

        [Test]
        public void AStairsPaceIsAboutThreeStepsASecondAtAWalk()
        {
            float stepsASecond = StairGait.WalkPace / (StairGait.StrideMetres * 0.5f);
            Assert.AreEqual(3.0f, stepsASecond, 0.01f);
        }
    }
}
