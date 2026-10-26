using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The Wraith's drifting (WraithDrift, pure): level headings turned no faster than it turns, drifts and lingers, home, the bob.</summary>
    public sealed class WraithDriftTests
    {
        private const float Dt = 1.0f / 60.0f;

        [Test]
        public void ItsHeadingStaysLevelAndUnitAndTurnsNoFasterThanItTurns()
        {
            var drift = new WraithDrift(3, Vector3.zero);
            Vector3 position = Vector3.zero;
            Vector3 before = drift.Heading;
            for (int i = 0; i < 60 * 120; i++)
            {
                position += drift.Tick(position, Dt) * Dt;
                Assert.AreEqual(0.0f, drift.Heading.y, 1e-6f, "its heading left the level");
                Assert.AreEqual(1.0f, drift.Heading.magnitude, 1e-4f, "its heading is not a unit vector");
                Assert.LessOrEqual(Vector3.Angle(before, drift.Heading), WraithDrift.TurnDegreesPerSecond * Dt + 0.01f, "it snapped round");
                before = drift.Heading;
            }
        }

        [Test]
        public void ItDriftsAndLingersInTurnGlidingBetweenThem()
        {
            var drift = new WraithDrift(11, Vector3.zero);
            Vector3 position = Vector3.zero;
            int lingering = 0, drifting = 0, hanging = 0;
            float speedBefore = drift.Speed;
            for (int i = 0; i < 60 * 120; i++)
            {
                Vector3 velocity = drift.Tick(position, Dt);
                position += velocity * Dt;
                Assert.LessOrEqual(drift.Speed, WraithDrift.CruiseSpeed + 1e-4f, "it outran its cruising speed");
                Assert.LessOrEqual(Mathf.Abs(drift.Speed - speedBefore), WraithDrift.Acceleration * Dt + 1e-4f, "it jumped to speed");
                Assert.AreEqual(drift.Speed, velocity.magnitude, 1e-4f);
                speedBefore = drift.Speed;
                if (drift.IsLingering)
                {
                    lingering++;
                    hanging += drift.Speed < 1e-4f ? 1 : 0;
                }
                else
                {
                    drifting++;
                }
            }
            Assert.Greater(lingering, 60 * 10, "in two minutes it never lingered");
            Assert.Greater(drifting, 60 * 30, "in two minutes it hardly drifted");
            Assert.Greater(hanging, 60 * 5, "lingering, it never came to a hang");
        }

        [Test]
        public void FarFromHomeItHeadsHome()
        {
            var home = new Vector3(100.0f, 0.0f, 100.0f);
            var drift = new WraithDrift(5, home);
            Vector3 away = home + new Vector3(WraithDrift.HomeRadius + 20.0f, 0.0f, 0.0f);
            for (int i = 0; i < 60 * 6; i++)
            {
                drift.Tick(away, Dt);
            }
            Assert.IsFalse(drift.IsLingering, "it hung about out past its home radius");
            Assert.Less(Vector3.Angle(drift.Heading, Vector3.left), 1.0f, "it is not heading home");
            Assert.AreEqual(WraithDrift.CruiseSpeed, drift.Speed, 1e-3f, "it is not drifting home at speed");
        }

        [Test]
        public void ItBobsOverItsHoverHeight()
        {
            Assert.AreEqual(0.0f, WraithDrift.Bob(0.0f), 1e-5f);
            Assert.AreEqual(WraithDrift.BobAmplitude, WraithDrift.Bob(WraithDrift.BobSeconds * 0.25f), 1e-4f);
            Assert.AreEqual(-WraithDrift.BobAmplitude, WraithDrift.Bob(WraithDrift.BobSeconds * 0.75f), 1e-4f);
            float lowest = float.MaxValue, highest = float.MinValue;
            for (float t = 0.0f; t < WraithDrift.BobSeconds * 2.0f; t += 0.01f)
            {
                float y = WraithDrift.HoverTargetY(5.0f, t);
                lowest = Mathf.Min(lowest, y);
                highest = Mathf.Max(highest, y);
            }
            Assert.AreEqual(5.0f + WraithDrift.HoverHeight - WraithDrift.BobAmplitude, lowest, 1e-3f);
            Assert.AreEqual(5.0f + WraithDrift.HoverHeight + WraithDrift.BobAmplitude, highest, 1e-3f);
        }

        [Test]
        public void TheSameSeedDriftsTheSameWayAndAnotherDiffers()
        {
            var one = new WraithDrift(42, Vector3.zero);
            var same = new WraithDrift(42, Vector3.zero);
            var other = new WraithDrift(43, Vector3.zero);
            Vector3 a = Vector3.zero, b = Vector3.zero, c = Vector3.zero;
            for (int i = 0; i < 60 * 40; i++)
            {
                a += one.Tick(a, Dt) * Dt;
                b += same.Tick(b, Dt) * Dt;
                c += other.Tick(c, Dt) * Dt;
            }
            Assert.AreEqual(a, b, "the same seed drifted two ways");
            Assert.Greater(Vector3.Distance(a, c), 1.0f, "two seeds drifted the same way");
        }
    }
}
