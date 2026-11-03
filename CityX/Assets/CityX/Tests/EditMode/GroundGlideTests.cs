using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The drawn body's glide over steps on paper (Character.GroundGlide): nothing on the flat, a steady line up a flight whose capsule lurches, and back on the capsule in the air.</summary>
    public sealed class GroundGlideTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const float Skin = 0.08f;
        private const float Tread = 0.36f;
        private const float Riser = 0.227f;

        private static float StairAt(float z) => z <= 0.0f ? 0.0f : Riser * (Mathf.Floor(z / Tread) + 1.0f);

        private static float[] Read(System.Func<float, float> ground, float z, float lead)
        {
            var heights = new float[GroundGlide.SampleAlong.Length];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = ground(z + GroundGlide.SampleAlong[i] + lead);
            }
            return heights;
        }

        [Test]
        public void OnTheFlatTheBodyIsTheCapsule()
        {
            var glide = new GroundGlide();
            for (int i = 0; i < 60; i++)
            {
                glide.Step(Skin, Read(z => 0.0f, i * 0.07f, 0.24f), Skin, 0.24f, true, Dt);
            }
            Assert.AreEqual(0.0f, glide.Offset, 1e-5f);
            Assert.AreEqual(0.0f, glide.Slope, 1e-5f);
        }

        [Test]
        public void UpAFlightTheBodyClimbsSteadilyWhileTheCapsuleLurches()
        {
            var glide = new GroundGlide();
            const float speed = 4.0f;
            float lead = speed * GroundGlide.EaseSeconds;
            float last = 0.0f, worstCapsule = 0.0f, worstBody = 0.0f, lastCapsule = 0.0f;
            float rate = speed * Dt * Riser / Tread;
            for (int i = 0; i < 90; i++)
            {
                float z = i * speed * Dt;
                float capsule = StairAt(z - 0.1f) + Skin;   // what a capsule does: on one tread until it is on the next
                glide.Step(capsule, Read(StairAt, z, lead), Skin, lead, true, Dt);
                float body = capsule + glide.Offset;
                if (i > 45)
                {
                    worstCapsule = Mathf.Max(worstCapsule, Mathf.Abs(capsule - lastCapsule - rate));
                    worstBody = Mathf.Max(worstBody, Mathf.Abs(body - last - rate));
                }
                last = body;
                lastCapsule = capsule;
            }
            Assert.Greater(worstCapsule, 0.1f, "setup: this capsule did not lurch");
            Assert.Less(worstBody, 0.05f, "the body lurched with the capsule");
            Assert.AreEqual(Riser / Tread, glide.Slope, 0.1f, "the slope of the flight was not read past its steps");
        }

        [Test]
        public void AFlightIsStairsAndARampOfTheSameSlopeIsNot()
        {
            var flight = new GroundGlide();
            var ramp = new GroundGlide();
            bool flightSeen = false, rampSeen = false;
            for (int i = 0; i < 90; i++)
            {
                float z = i * 4.0f * Dt;
                flight.Step(StairAt(z) + Skin, Read(StairAt, z, 0.0f), Skin, 0.0f, true, Dt);
                ramp.Step(z * (Riser / Tread) + Skin, Read(at => at * (Riser / Tread), z, 0.0f), Skin, 0.0f, true, Dt);
                flightSeen |= flight.IsStairs;
                rampSeen |= ramp.IsStairs;
            }
            Assert.IsTrue(flightSeen, "a flight was not taken for stairs");
            Assert.IsFalse(rampSeen, "a ramp was taken for stairs");
        }

        [Test]
        public void AKerbIsNotStairs()
        {
            var glide = new GroundGlide();
            for (int i = 0; i < 90; i++)
            {
                float z = i * 4.0f * Dt - 2.0f;
                glide.Step((z > 0.0f ? 0.2f : 0.0f) + Skin, Read(at => at > 0.0f ? 0.2f : 0.0f, z, 0.0f), Skin, 0.0f, true, Dt);
                Assert.IsFalse(glide.IsStairs, "a kerb was taken for stairs");
            }
        }

        [Test]
        public void AWallAheadIsNotTheWayOn()
        {
            var glide = new GroundGlide();
            for (int i = 0; i < 60; i++)
            {
                glide.Step(Skin, Read(z => z > 0.3f ? 3.0f : 0.0f, 0.0f, 0.0f), Skin, 0.0f, true, Dt);
            }
            Assert.AreEqual(0.0f, glide.Offset, 1e-5f);
            Assert.AreEqual(0.0f, glide.Slope, 1e-5f);
        }

        [Test]
        public void NoGroundIsNoGlide()
        {
            var glide = new GroundGlide();
            glide.Step(Skin, new[] { float.NaN, float.NaN, float.NaN, float.NaN }, Skin, 0.0f, true, Dt);
            Assert.AreEqual(0.0f, glide.Offset);
        }

        [Test]
        public void InTheAirTheBodyGoesBackOntoTheCapsuleAndDoesNotTrailAJump()
        {
            var glide = new GroundGlide();
            for (int i = 0; i < 60; i++)
            {
                glide.Step(1.0f + Skin, Read(StairAt, 1.0f, 0.0f), Skin, 0.0f, true, Dt);
            }
            float before = Mathf.Abs(glide.Offset);
            float y = 1.0f + Skin;
            for (int i = 0; i < 20; i++)
            {
                y += 5.0f * Dt;
                glide.Step(y, Read(StairAt, 1.0f, 0.0f), Skin, 0.0f, false, Dt);
                Assert.LessOrEqual(Mathf.Abs(glide.Offset), before + 1e-5f, "a jump's capsule left the body behind");
            }
            Assert.AreEqual(0.0f, glide.Offset, 0.002f);
        }

        [Test]
        public void ItNeverDrawsTheBodyFurtherFromTheCapsuleThanTheLimit()
        {
            var glide = new GroundGlide();
            for (int i = 0; i < 120; i++)
            {
                glide.Step(Skin, new[] { 0.7f, 0.7f, 0.7f, 0.6f }, Skin, 0.0f, true, Dt);
                Assert.LessOrEqual(Mathf.Abs(glide.Offset), GroundGlide.MaxOffset + 1e-5f);
            }
        }
    }
}
