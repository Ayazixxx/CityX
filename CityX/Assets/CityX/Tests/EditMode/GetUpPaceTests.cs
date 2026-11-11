using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The pace of the ragdoll's get-up (GetUpPace), and how far the ground under a hand or foot moves it (Ragdoll.OnTheGroundShare).
    /// </summary>
    public sealed class GetUpPaceTests
    {
        private const float Frame = 1.0f / 60.0f;

        [Test]
        public void TheQuickGetUpRunsTheClipAtOverTwiceItsSpeedToItsEnd()
        {
            float clip = GetUpMotion.Seconds(GetUpMotion.FromTheBack);
            var pace = new GetUpPace(GetUpPace.QuickSpeed, clip);
            Assert.AreEqual(clip / GetUpPace.QuickSpeed, pace.Seconds, 1e-4f);
            Assert.AreEqual(0.0f, pace.ClipSecondsAt(-1.0f), 1e-5f, "the clip runs before the roll is done");
            float last = 0.0f;
            for (float t = Frame; t <= pace.Seconds + 0.5f; t += Frame)
            {
                float at = pace.ClipSecondsAt(t);
                Assert.GreaterOrEqual(at, last, "the clip ran backwards");
                Assert.LessOrEqual(at, clip, "the clip ran past its end");
                last = at;
            }
            Assert.AreEqual(clip, pace.ClipSecondsAt(pace.Seconds), 1e-4f, "it is handed back before the clip is over");
            Assert.That(pace.Seconds, Is.InRange(1.2f, 2.2f), "a knockdown in play keeps the body down too long, or pops it up");
            Assert.LessOrEqual(Ragdoll.StandUpRecoverySeconds, 2.5f, "control comes back far later than it did (1.6 s from the back)");
        }

        [Test]
        public void RollingOverTakesLongerTheFurtherRoundTheBodyLies()
        {
            var pace = new GetUpPace(GetUpPace.QuickSpeed, 3.0f);
            Assert.AreEqual(0.0f, pace.RollSeconds(0.0f), "a body lying as the clip starts is given time to roll");
            Assert.AreEqual(pace.HalfTurnSeconds, pace.RollSeconds(180.0f), 1e-5f);
            Assert.AreEqual(pace.HalfTurnSeconds, pace.RollSeconds(-180.0f), 1e-5f, "rolling the other way round is timed differently");
            Assert.AreEqual(pace.HalfTurnSeconds * 0.5f, pace.RollSeconds(90.0f), 1e-5f);
            Assert.That(pace.HalfTurnSeconds, Is.InRange(0.3f, 0.5f), "in a fight, the roll is a blink or a wait");
            Assert.Greater(new GetUpPace(IntroCutscene.GettingUpSpeed, 3.0f).HalfTurnSeconds, 0.8f, "the entrance hurries the roll");
        }

        [Test]
        public void AHandOnTheFloorGoesWithTheGroundUnderItAndOneInTheAirDoesNot()
        {
            Assert.AreEqual(1.0f, Ragdoll.OnTheGroundShare(0.1f), 1e-4f, "a hand on the floor does not go onto the step under it");
            Assert.AreEqual(0.0f, Ragdoll.OnTheGroundShare(Ragdoll.OffTheFloor + 0.1f), 1e-5f, "a hand in the air is moved by the ground under it");
            Assert.That(Ragdoll.OnTheGroundShare(0.5f * (Ragdoll.OnTheFloor + Ragdoll.OffTheFloor)), Is.InRange(0.3f, 0.7f));
        }
    }
}
