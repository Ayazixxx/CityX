using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Another player's body played back from the moments its owner sent (docs/MULTIPLAYER_MOVEMENT.md; owner after the first online duel,
    /// 2026-11-13: "really kinda choppy and laggy"): steady however unevenly the moments were taken, sent or received, a known delay
    /// behind, never past what it has been told, and standing until the body really set off.
    /// </summary>
    public sealed class BodyMotionPlaybackTests
    {
        private const float Frame = 1.0f / 60.0f;
        private const float WalkSpeed = 4.0f;

        private static BodyMoment At(double time, float x) =>
            new BodyMoment { Time = time, Position = new Vector3(x, 0.0f, 0.0f), Rotation = Quaternion.identity, Scale = Vector3.one };

        private static BodyMoment Walking(double time) => At(time, (float)(time * WalkSpeed));

        /// <summary>
        /// Plays <paramref name="arrivals"/> (each a moment and the time, on this machine, it arrives) for <paramref name="seconds"/>, a frame
        /// at a time; what was shown each frame.
        /// </summary>
        private static List<BodyMoment> Play(BodyMotionPlayback playback, List<(double arrives, BodyMoment moment)> arrivals, float seconds)
        {
            var shown = new List<BodyMoment>();
            int next = 0;
            arrivals.Sort((a, b) => a.arrives.CompareTo(b.arrives));
            for (double now = 0.0; now < seconds; now += Frame)
            {
                while (next < arrivals.Count && arrivals[next].arrives <= now)
                {
                    playback.Add(arrivals[next].moment);
                    next++;
                }
                if (playback.HasAny)
                {
                    shown.Add(playback.Advance(Frame));
                }
            }
            return shown;
        }

        /// <summary>A walk told at uneven moments (as a body moved every frame, at whatever frame rate, is), each arriving a little late.</summary>
        private static List<(double, BodyMoment)> UnevenWalk(float seconds, System.Func<int, bool> lost = null)
        {
            var random = new System.Random(7);
            var arrivals = new List<(double, BodyMoment)>();
            double time = 0.0;
            for (int i = 0; time < seconds; i++)
            {
                if (lost == null || !lost(i))
                {
                    arrivals.Add((time + 0.005 + random.NextDouble() * 0.03, Walking(time)));
                }
                time += 0.015 + random.NextDouble() * 0.02;
            }
            return arrivals;
        }

        private static void AssertSteady(List<BodyMoment> shown, int fromFrame, string why)
        {
            for (int i = fromFrame + 1; i < shown.Count; i++)
            {
                float speed = (shown[i].Position.x - shown[i - 1].Position.x) / Frame;
                Assert.That(speed, Is.InRange(WalkSpeed * 0.75f, WalkSpeed * 1.25f), $"{why}: frame {i} was shown at {speed:F2} m/s of a {WalkSpeed} m/s walk");
            }
        }

        [Test]
        public void ASteadyWalkIsShownSteadyHoweverUnevenlyItWasTold()
        {
            var playback = new BodyMotionPlayback();
            List<BodyMoment> shown = Play(playback, UnevenWalk(4.0f), 4.0f);
            AssertSteady(shown, 60, "moments taken 15 to 35 ms apart and arriving 5 to 35 ms late");
            // Where it is shown is where the body was at the time it is shown at, not near it.
            BodyMoment last = shown[shown.Count - 1];
            Assert.AreEqual(last.Time * WalkSpeed, last.Position.x, 1e-3, "the copy is not where its owner was at the time shown");
        }

        [Test]
        public void ALostPacketLeavesNoStallAndNoLurch()
        {
            var playback = new BodyMotionPlayback();
            List<BodyMoment> shown = Play(playback, UnevenWalk(4.0f, i => i % 9 == 4), 4.0f);
            AssertSteady(shown, 60, "one moment in nine lost");
        }

        [Test]
        public void ItIsShownTheDelayBehindTheNewestMoment()
        {
            var playback = new BodyMotionPlayback();
            var arrivals = new List<(double, BodyMoment)>();
            for (int i = 0; i < 120; i++)
            {
                arrivals.Add((i / 30.0, Walking(i / 30.0)));
            }
            Play(playback, arrivals, 4.0f);
            double newest = 119 / 30.0;
            Assert.AreEqual(BodyMotionPlayback.DelaySeconds, newest - playback.ShownTime, 0.04, "the copy is not shown the delay behind what it was last told");
        }

        [Test]
        public void MomentsArrivingOutOfOrderAreShownInOrder()
        {
            var playback = new BodyMotionPlayback();
            var arrivals = new List<(double, BodyMoment)>();
            for (int i = 0; i < 90; i++)
            {
                // Every third pair arrives swapped.
                double arrives = i / 30.0 + (i % 3 == 0 ? 0.04 : 0.0);
                arrivals.Add((arrives, Walking(i / 30.0)));
            }
            List<BodyMoment> shown = Play(playback, arrivals, 3.0f);
            AssertSteady(shown, 30, "moments arriving out of order");
        }

        [Test]
        public void ItNeverRunsPastWhatItWasTold()
        {
            var playback = new BodyMotionPlayback();
            var arrivals = new List<(double, BodyMoment)>();
            for (int i = 0; i <= 30; i++)
            {
                arrivals.Add((i / 30.0, Walking(i / 30.0)));
            }
            List<BodyMoment> shown = Play(playback, arrivals, 3.0f);   // told for one second, played for three
            BodyMoment last = shown[shown.Count - 1];
            Assert.AreEqual(1.0, last.Time, 1e-6, "the playback ran on past the newest moment");
            Assert.AreEqual(WalkSpeed, last.Position.x, 1e-4, "the copy does not stand where its owner last said it was");
        }

        [Test]
        public void ABodySettingOffAfterStandingIsShownStandingUntilItSetOffAndThenWalksSteadily()
        {
            var playback = new BodyMotionPlayback();
            var arrivals = new List<(double, BodyMoment)>();
            // Standing at x = 0, told five times a second; it sets off at 1.0, saying first that it stood there until then.
            for (int i = 0; i <= 4; i++)
            {
                arrivals.Add((i * 0.2 + 0.01, At(i * 0.2, 0.0f)));
            }
            arrivals.Add((1.0 + 0.01, At(1.0, 0.0f)));
            for (int i = 1; i <= 60; i++)
            {
                double time = 1.0 + i / 30.0;
                arrivals.Add((time + 0.01, At(time, (float)((time - 1.0) * WalkSpeed))));
            }
            List<BodyMoment> shown = Play(playback, arrivals, 3.0f);
            int setOff = -1;
            for (int i = 0; i < shown.Count; i++)
            {
                if (shown[i].Time <= 1.0)
                {
                    Assert.AreEqual(0.0f, shown[i].Position.x, 1e-5f, $"the copy slid off at {shown[i].Time:F3}, before its owner set off at 1.0");
                }
                else if (setOff < 0)
                {
                    setOff = i;
                }
            }
            Assert.Greater(setOff, 0, "the copy never set off");
            // From its first step it has the whole delay ahead of it: no stutter while that builds back up.
            for (int i = setOff + 2; i < shown.Count; i++)
            {
                float speed = (shown[i].Position.x - shown[i - 1].Position.x) / Frame;
                Assert.That(speed, Is.InRange(WalkSpeed * 0.75f, WalkSpeed * 1.25f), $"frame {i} after setting off was shown at {speed:F2} m/s of a {WalkSpeed} m/s walk");
            }
        }

        [Test]
        public void ATeleportIsNotSlidAcross()
        {
            var playback = new BodyMotionPlayback();
            var arrivals = new List<(double, BodyMoment)>();
            for (int i = 0; i < 60; i++)
            {
                arrivals.Add((i / 30.0, At(i / 30.0, i < 30 ? 0.0f : 500.0f)));
            }
            foreach (BodyMoment moment in Play(playback, arrivals, 2.5f))
            {
                Assert.IsTrue(moment.Position.x < 0.01f || moment.Position.x > 499.99f, $"a teleported body was shown on the way, at x = {moment.Position.x:F1}");
            }
        }

        [Test]
        public void NumbersNoBodyCouldHaveAreNotTakenOffTheWire()
        {
            var turn = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
            Assert.IsTrue(NetworkedBodyMotion.TryRead(1.0, Vector3.one, turn, Vector3.one, out _));
            Assert.IsFalse(NetworkedBodyMotion.TryRead(double.NaN, Vector3.one, turn, Vector3.one, out _), "a time that is not a number");
            Assert.IsFalse(NetworkedBodyMotion.TryRead(1.0, new Vector3(float.NaN, 0.0f, 0.0f), turn, Vector3.one, out _), "a place that is not a number");
            Assert.IsFalse(NetworkedBodyMotion.TryRead(1.0, Vector3.one, new Vector4(0.0f, 0.0f, 0.0f, 3.0f), Vector3.one, out _), "a turn that is no rotation");
            Assert.IsFalse(NetworkedBodyMotion.TryRead(1.0, Vector3.one, turn, Vector3.zero, out _), "a body of no size");
            Assert.IsFalse(NetworkedBodyMotion.TryRead(1.0, Vector3.one, turn, Vector3.one * 5000.0f, out _), "a body the size of the city");
        }
    }
}
