using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The pace of the ragdoll's get-up (GetUpPace; owner, 2026-11-10: "make the standing up animation look good ... right now it looks
    /// glitchy"), and how far the ground under a hand or foot moves it (LimbFloorClamp.Follows).
    /// </summary>
    public sealed class GetUpPaceTests
    {
        private const float Frame = 1.0f / 60.0f;

        [Test]
        public void TheQuickGetUpSkipsTheShiftingAndGivesTheRiseToStandingItsTime()
        {
            var pace = new GetUpPace(Ragdoll.StandUpPaceSeconds);
            Assert.AreEqual(Ragdoll.StandUpRecoverySeconds, pace.Seconds, 1e-4f, "control comes back at another time than it did");
            Assert.Greater(pace.ClipSecondsAt(0.0f), 0.8f, "it still lies shifting about before it pushes up");
            Assert.AreEqual(pace.ClipSecondsAt(0.0f), pace.ClipSecondsAt(-1.0f), 1e-5f, "the clip runs before the body is on its front");

            float rise = 0.0f, last = pace.ClipSecondsAt(0.0f);
            for (float t = Frame; t <= pace.Seconds + 1e-4f; t += Frame)
            {
                float clip = pace.ClipSecondsAt(t);
                Assert.GreaterOrEqual(clip, last, "the clip ran backwards");
                if (clip > 4.0f && last < 4.9f)
                {
                    rise += Frame;   // from its feet under it to standing
                }
                last = clip;
            }
            Assert.That(pace.ClipSecondsAt(pace.Seconds), Is.InRange(4.8f, GetUpPace.UprightClipSeconds), "it is handed back before or long after it is up");
            Assert.Greater(rise, 0.35f, "the rise to standing is a pop (it took a fifth of a second played evenly)");
        }

        [Test]
        public void TheEntrancePlaysTheClipEvenlyAtItsOwnPace()
        {
            var pace = new GetUpPace(GetUpPace.ClipSeconds);
            Assert.AreEqual(GetUpPace.UprightClipSeconds, pace.Seconds, 1e-4f);
            for (float t = 0.0f; t <= pace.Seconds; t += 0.5f)
            {
                Assert.AreEqual(t, pace.ClipSecondsAt(t), 1e-4f, "the slow get-up is not the clip as it is");
            }
        }

        [Test]
        public void RollingOverTakesLongerTheFurtherRoundTheBodyLies()
        {
            var pace = new GetUpPace(Ragdoll.StandUpPaceSeconds);
            Assert.AreEqual(0.0f, pace.RollSeconds(0.0f), "a body on its front is given time to roll");
            Assert.AreEqual(pace.HalfTurnSeconds, pace.RollSeconds(180.0f), 1e-5f);
            Assert.AreEqual(pace.HalfTurnSeconds, pace.RollSeconds(-180.0f), 1e-5f, "rolling the other way round is timed differently");
            Assert.AreEqual(pace.HalfTurnSeconds * 0.5f, pace.RollSeconds(90.0f), 1e-5f);
            Assert.That(pace.HalfTurnSeconds, Is.InRange(0.3f, 0.5f), "from its back, in a fight, the roll is a blink or a wait");
            Assert.Greater(new GetUpPace(GetUpPace.ClipSeconds).HalfTurnSeconds, 1.2f, "the entrance hurries the roll");
        }

        [Test]
        public void AHandOnTheFloorFollowsTheGroundUnderItAndOneInTheAirDoesNot()
        {
            const float ClipFloor = 10.0f, Step = 0.3f;
            var onTheFloor = new Vector3(0.0f, ClipFloor + 0.1f, 0.0f);
            Assert.AreEqual(Step, LimbFloorClamp.Follows(onTheFloor, ClipFloor + Step, ClipFloor), 1e-4f, "a hand on the floor does not go up onto the step under it");
            Assert.AreEqual(-Step, LimbFloorClamp.Follows(onTheFloor, ClipFloor - Step, ClipFloor), 1e-4f, "a hand on the floor does not go down onto the step under it");
            var inTheAir = new Vector3(0.0f, ClipFloor + LimbFloorClamp.OffTheFloor + 0.1f, 0.0f);
            Assert.AreEqual(0.0f, LimbFloorClamp.Follows(inTheAir, ClipFloor + Step, ClipFloor), 1e-5f, "a hand in the air is moved by the ground under it");
            var between = new Vector3(0.0f, ClipFloor + 0.5f * (LimbFloorClamp.OnTheFloor + LimbFloorClamp.OffTheFloor), 0.0f);
            Assert.That(LimbFloorClamp.Follows(between, ClipFloor + Step, ClipFloor), Is.InRange(0.3f * Step, 0.7f * Step));
        }
    }
}
