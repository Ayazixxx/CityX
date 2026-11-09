using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Recovery flip's shape (RecoveryFlip; owner, 2026-11-08: "make the recover skill work like the recovery in saints row 4"): from however
    /// the body lies or tumbles it turns a real flip to upright, the way it is already spinning, faces the way it lies, keeps its momentum, and
    /// is sent up enough to finish the flip in the air when it would land first.
    /// </summary>
    public sealed class RecoveryFlipTests
    {
        private const float Gravity = 9.8f;
        private static readonly Vector3 Right = Vector3.right;

        /// <summary>A body pitched <paramref name="degrees"/> forward about its right (+x), facing +z: from its hips to its neck.</summary>
        private static Vector3 UpPitched(float degrees) => Quaternion.AngleAxis(degrees, Vector3.right) * Vector3.up;

        private static RecoveryFlip Plan(float pitch, float spinDegreesPerSecond = 0.0f, Vector3 velocity = default, float height = 20.0f) =>
            RecoveryFlip.Plan(UpPitched(pitch), Right, Right * (spinDegreesPerSecond * Mathf.Deg2Rad), velocity, height, Gravity, Vector3.forward);

        [Test]
        public void ItAlwaysEndsUprightAfterARealFlip([Values(0.0f, 30.0f, 90.0f, 170.0f, 190.0f, 270.0f, 330.0f)] float pitch)
        {
            foreach (float spin in new[] { 0.0f, 600.0f, -600.0f })
            {
                RecoveryFlip flip = Plan(pitch, spin);
                float end = flip.PitchAt(flip.Seconds);
                Assert.AreEqual(0.0f, Mathf.DeltaAngle(0.0f, end), 0.01f, $"pitched {pitch}, spinning {spin}: not upright at the end");
                Assert.AreEqual(flip.StartDegrees, flip.PitchAt(0.0f), 0.01f, "it begins where the body is");
                Assert.GreaterOrEqual(Mathf.Abs(flip.SweepDegrees), RecoveryFlip.MinSweepDegrees - 0.01f, $"pitched {pitch}, spinning {spin}: a nudge, not a flip");
                Assert.LessOrEqual(Mathf.Abs(flip.SweepDegrees), 540.0f);
            }
        }

        [Test]
        public void AStillBodyFlipsTheLongWayRoundAndAnUprightOneTurnsAWholeTurnForward()
        {
            Assert.AreEqual(360.0f, Plan(0.0f).SweepDegrees, 0.01f, "upright: a whole forward flip");
            Assert.AreEqual(270.0f, Plan(90.0f).SweepDegrees, 0.01f, "diving head first: on over, forward");
            Assert.AreEqual(-270.0f, Plan(270.0f).SweepDegrees, 0.01f, "lying on its back, head behind: over backwards");
            Assert.That(Plan(170.0f).SweepDegrees, Is.EqualTo(190.0f).Within(0.01f), "head down, a little forward: forward");
            Assert.That(Plan(190.0f).SweepDegrees, Is.EqualTo(-190.0f).Within(0.01f), "head down, a little back: backwards");
        }

        [Test]
        public void ASpinningBodyFlipsOnTheWayItSpins()
        {
            Assert.Greater(Plan(200.0f, 500.0f).SweepDegrees, 0.0f, "tumbling forward, head just past down: it carries on forward");
            Assert.Less(Plan(160.0f, -500.0f).SweepDegrees, 0.0f, "tumbling backward: it carries on back");
            Assert.AreEqual(20.0f + 360.0f, Plan(340.0f, 500.0f).SweepDegrees, 0.01f, "tumbling forward and nearly upright already: round once more");
        }

        [Test]
        public void ItTurnsFastestWhenCurledAndOpensOutBeforeTheEnd()
        {
            RecoveryFlip flip = Plan(0.0f);
            float step = flip.Seconds / 20.0f;
            float early = flip.PitchAt(step) - flip.PitchAt(0.0f);
            float middle = flip.PitchAt(flip.Seconds * 0.5f + step * 0.5f) - flip.PitchAt(flip.Seconds * 0.5f - step * 0.5f);
            float late = flip.PitchAt(flip.Seconds) - flip.PitchAt(flip.Seconds - step);
            Assert.Greater(middle, early * 2.0f, "the curled middle turns far faster than the open start");
            Assert.Greater(middle, late * 2.0f, "and than the opened end");
            Assert.AreEqual(0.0f, flip.TuckAt(0.0f), 0.001f, "it starts open (the pose it was flung in)");
            Assert.AreEqual(1.0f, flip.TuckAt(flip.Seconds * 0.4f), 0.001f, "curled up in the middle");
            Assert.AreEqual(0.0f, flip.TuckAt(flip.Seconds * RecoveryFlip.TuckOutEnd), 0.001f, "legs down before it is round");
        }

        [Test]
        public void ItFacesTheWayTheBodyLiesAndTurnsAboutItsRight()
        {
            Vector3 east = Vector3.right;
            // A body lying face down, head to the east: its right points south.
            RecoveryFlip flip = RecoveryFlip.Plan(east, Vector3.back, Vector3.zero, Vector3.zero, 0.0f, Gravity, Vector3.forward);
            Assert.Less(Vector3.Angle(flip.Facing, east), 0.1f, "it should face the way its head lies");
            Assert.AreEqual(90.0f, flip.StartDegrees, 0.01f, "face down, head forward: pitched a quarter turn forward");
            Assert.AreEqual(0.0f, flip.Facing.y, 1e-5f);

            // On its side, its right pointing up: it faces where its head points.
            flip = RecoveryFlip.Plan(Vector3.forward, Vector3.up, Vector3.zero, Vector3.zero, 0.0f, Gravity, Vector3.right);
            Assert.Less(Vector3.Angle(flip.Facing, Vector3.forward), 0.1f);
        }

        [Test]
        public void ItKeepsItsMomentumAndIsSentUpOnlyWhenItWouldLandBeforeTheFlipIsRound()
        {
            var flung = new Vector3(9.0f, 3.0f, -4.0f);
            RecoveryFlip high = Plan(90.0f, 0.0f, flung, 30.0f);
            Assert.AreEqual(flung, high.Velocity, "thrown high, it flies on exactly as it was going");

            RecoveryFlip lying = Plan(90.0f, 0.0f, Vector3.zero, 0.0f);
            float airborne = 2.0f * lying.Velocity.y / Gravity;
            Assert.GreaterOrEqual(airborne, lying.Seconds + RecoveryFlip.UprightBeforeLandingSeconds - 0.001f,
                "lying on the ground, it should pop up high enough to finish the flip and be on its feet before it lands");
            Assert.AreEqual(0.0f, lying.Velocity.x, 1e-5f);

            RecoveryFlip falling = Plan(90.0f, 0.0f, new Vector3(5.0f, -12.0f, 0.0f), 1.0f);
            Assert.Greater(falling.Velocity.y, -12.0f, "falling fast a metre above the ground, it is held up long enough to land on its feet");
            Assert.AreEqual(5.0f, falling.Velocity.x, 1e-5f, "its way across is kept");
        }

        [Test]
        public void AWholeTurnTakesAboutHalfASecondAndAHalfTurnLess()
        {
            Assert.AreEqual(RecoveryFlip.FullTurnSeconds, Plan(0.0f).Seconds, 1e-4f);
            Assert.AreEqual(RecoveryFlip.HalfTurnSeconds, Plan(180.0f).Seconds, 1e-4f);
        }
    }
}
