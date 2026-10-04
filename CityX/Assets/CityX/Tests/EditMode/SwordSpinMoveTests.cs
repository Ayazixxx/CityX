using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The sword spin's timeline against the numbers it was taken from (Link's Spin Attack in Smash Ultimate, at 60 fps -- see
    /// docs/SWORD_SPIN.md): if one of these fails, the move has drifted from its source.
    /// </summary>
    public sealed class SwordSpinMoveTests
    {
        private const float Frame = 1.0f / 60.0f;

        [Test]
        public void TheWholeAnimationIs91Frames()
        {
            Assert.AreEqual(91.0f * Frame, SwordSpinMove.AnimationSeconds, 1e-5f);
            Assert.AreEqual(91.0f * Frame, SwordSpinMove.CommitSeconds(SpinKind.Air), 1e-5f);
        }

        [Test]
        public void TheGroundSpinIsInterruptibleAtFrame74()
        {
            Assert.AreEqual(74.0f * Frame, SwordSpinMove.CommitSeconds(SpinKind.Ground), 1e-5f);
        }

        [Test]
        public void TheGroundSpinHurtsFromFrame7ToFrame39()
        {
            Assert.AreEqual(0.0f, SwordSpinMove.GroundDamageScale(6.0f * Frame), "before the first hit frame");
            Assert.Greater(SwordSpinMove.GroundDamageScale(7.0f * Frame), 0.0f, "frame 7 is the first");
            Assert.Greater(SwordSpinMove.GroundDamageScale(39.0f * Frame), 0.0f, "frame 39 is the last");
            Assert.AreEqual(0.0f, SwordSpinMove.GroundDamageScale(40.0f * Frame), "after the last");
        }

        [Test]
        public void TheGroundSpinFadesFrom14To7InFourBands()
        {
            // clean 7-8: 14%, mid 9-13: 12%, late 14-23: 9%, latest 24-39: 7%
            foreach (int frame in new[] { 7, 8 }) Assert.AreEqual(1.4f, SwordSpinMove.GroundDamageScale(frame * Frame), 1e-4f, $"frame {frame}");
            foreach (int frame in new[] { 9, 11, 13 }) Assert.AreEqual(1.2f, SwordSpinMove.GroundDamageScale(frame * Frame), 1e-4f, $"frame {frame}");
            foreach (int frame in new[] { 14, 18, 23 }) Assert.AreEqual(0.9f, SwordSpinMove.GroundDamageScale(frame * Frame), 1e-4f, $"frame {frame}");
            foreach (int frame in new[] { 24, 30, 39 }) Assert.AreEqual(0.7f, SwordSpinMove.GroundDamageScale(frame * Frame), 1e-4f, $"frame {frame}");
        }

        [Test]
        public void ChargingAddsUpToSixtyPercentOverSixtyFrames()
        {
            Assert.AreEqual(1.0f, SwordSpinMove.ChargeMultiplier(0.0f), 1e-5f);
            Assert.AreEqual(1.3f, SwordSpinMove.ChargeMultiplier(30.0f * Frame), 1e-5f);
            Assert.AreEqual(1.6f, SwordSpinMove.ChargeMultiplier(60.0f * Frame), 1e-5f);
            Assert.AreEqual(1.6f, SwordSpinMove.ChargeMultiplier(120.0f * Frame), 1e-5f, "charge past the maximum counts for nothing");
        }

        [Test]
        public void TheAirSpinHitsEightTimesThenLaunches()
        {
            var pulses = SwordSpinMove.AirPulses;
            Assert.AreEqual(9, pulses.Length, "eight spin blows and the last");
            int[] frames = { 8, 12, 16, 19, 22, 26, 31, 38, 48 };
            for (int i = 0; i < frames.Length; i++)
            {
                Assert.AreEqual(frames[i] * Frame, pulses[i].Time, 1e-5f, $"blow {i}");
                Assert.AreEqual(i == frames.Length - 1, pulses[i].Final, $"blow {i} final");
            }
            // Front 4%, back 2%, alternating.
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(i % 2 == 0 ? 0.4f : 0.2f, pulses[i].DamageScale, 1e-5f, $"blow {i}");
            }
            Assert.Greater(pulses[8].DamageScale, pulses[0].DamageScale, "the launching blow is the strong one");
        }

        [Test]
        public void TheAirSpinsBlowsAreInOrderAndInsideItsActiveTime()
        {
            var pulses = SwordSpinMove.AirPulses;
            for (int i = 1; i < pulses.Length; i++)
            {
                Assert.Greater(pulses[i].Time, pulses[i - 1].Time, $"blow {i} is not after blow {i - 1}");
            }
            Assert.LessOrEqual(pulses[pulses.Length - 1].Time, SwordSpinMove.AirActiveSeconds);
        }

        [Test]
        public void LandingFromTheAirSpinCostsThirtyFrames()
        {
            Assert.AreEqual(30.0f * Frame, SwordSpinMove.AirLandingLagSeconds, 1e-5f);
        }

        [TestCase(SpinKind.Ground)]
        [TestCase(SpinKind.Air)]
        public void TheTurnEndsOnWholeRevolutions(SpinKind kind)
        {
            float end = SwordSpinMove.AngleAt(kind, SwordSpinMove.TurnSeconds(kind));
            Assert.AreEqual(SwordSpinMove.TotalDegrees(kind), end, 1e-2f);
            Assert.AreEqual(0.0f, end % 360.0f, 1e-2f, "the body should end facing where it began");
            Assert.AreEqual(end, SwordSpinMove.AngleAt(kind, 99.0f), 1e-2f, "and stay there");
            Assert.AreEqual(0.0f, SwordSpinMove.AngleAt(kind, 0.0f), 1e-4f);
        }

        [TestCase(SpinKind.Ground)]
        [TestCase(SpinKind.Air)]
        public void TheTurnNeverRunsBackwards(SpinKind kind)
        {
            float last = 0.0f;
            float turn = SwordSpinMove.TurnSeconds(kind);
            for (int i = 1; i <= 600; i++)
            {
                float angle = SwordSpinMove.AngleAt(kind, turn * i / 600.0f);
                Assert.GreaterOrEqual(angle, last - 1e-3f, $"{kind} turned back at step {i}");
                last = angle;
            }
        }

        /// <summary>Slow and undulating on the ground: its speed really does swell and dip (not a steady turn), but never stops.</summary>
        [Test]
        public void TheGroundSpinsSpeedSwellsAndDips()
        {
            float turn = SwordSpinMove.TurnSeconds(SpinKind.Ground);
            float slowest = float.MaxValue, fastest = 0.0f;
            for (int i = 0; i < 600; i++)
            {
                float a0 = SwordSpinMove.AngleAt(SpinKind.Ground, turn * i / 600.0f);
                float a1 = SwordSpinMove.AngleAt(SpinKind.Ground, turn * (i + 1) / 600.0f);
                float speed = (a1 - a0) / (turn / 600.0f);
                slowest = UnityEngine.Mathf.Min(slowest, speed);
                fastest = UnityEngine.Mathf.Max(fastest, speed);
            }
            Assert.Greater(slowest, 0.0f, "it must never stall");
            Assert.Greater(fastest / slowest, 2.0f, "the undulation should be plain: fastest well over twice the slowest");
        }

        /// <summary>The air spin is the fast one: it turns faster, on average, than the ground one.</summary>
        [Test]
        public void TheAirSpinTurnsFasterThanTheGroundSpin()
        {
            float ground = SwordSpinMove.TotalDegrees(SpinKind.Ground) / SwordSpinMove.TurnSeconds(SpinKind.Ground);
            float air = SwordSpinMove.TotalDegrees(SpinKind.Air) / SwordSpinMove.TurnSeconds(SpinKind.Air);
            Assert.Greater(air, ground * 1.4f);
        }
    }
}
