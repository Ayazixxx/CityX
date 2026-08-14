using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The claim cutscene's framing and orbit maths, ported straight from
    /// player/claim_cutscene.gd. Both engines are Y-up, so unlike a
    /// mirrored model this carries over without adaptation -- what matters
    /// here is that the port is a faithful copy, not a reinterpretation.
    /// </summary>
    public sealed class ClaimCutsceneMathTests
    {
        [Test]
        public void FrameDistanceNeverGoesBelowTheMinimum()
        {
            float distance = ClaimCutsceneMath.FrameDistance(
                new Vector3(1.0f, 1.0f, 1.0f), fovDegrees: 75.0f, margin: 1.18f, minDistance: 16.0f);

            Assert.AreEqual(16.0f, distance, 0.001f, "a tiny building should still be framed from the minimum distance");
        }

        [Test]
        public void FrameDistanceGrowsWithTheBuilding()
        {
            float small = ClaimCutsceneMath.FrameDistance(
                new Vector3(12.0f, 20.0f, 12.0f), fovDegrees: 75.0f, margin: 1.18f, minDistance: 16.0f);
            float large = ClaimCutsceneMath.FrameDistance(
                new Vector3(40.0f, 80.0f, 40.0f), fovDegrees: 75.0f, margin: 1.18f, minDistance: 16.0f);

            Assert.Greater(large, small, "a bigger building should need a farther camera to stay framed");
        }

        [Test]
        public void PlaceOnOrbitAtZeroElevationAndAngleSitsOnTheXAxis()
        {
            Vector3 position = ClaimCutsceneMath.PlaceOnOrbit(
                focus: Vector3.zero, startAngle: 0.0f, angle: 0.0f, elevationDegrees: 0.0f, distance: 10.0f);

            Assert.AreEqual(new Vector3(10.0f, 0.0f, 0.0f), position);
        }

        [Test]
        public void PlaceOnOrbitAQuarterTurnSitsOnTheZAxis()
        {
            Vector3 position = ClaimCutsceneMath.PlaceOnOrbit(
                focus: Vector3.zero, startAngle: 0.0f, angle: Mathf.PI * 0.5f, elevationDegrees: 0.0f, distance: 10.0f);

            Assert.AreEqual(0.0f, position.x, 0.001f);
            Assert.AreEqual(10.0f, position.z, 0.001f);
        }

        [Test]
        public void PlaceOnOrbitRisesWithElevation()
        {
            Vector3 flat = ClaimCutsceneMath.PlaceOnOrbit(Vector3.zero, 0.0f, 0.0f, 0.0f, 10.0f);
            Vector3 elevated = ClaimCutsceneMath.PlaceOnOrbit(Vector3.zero, 0.0f, 0.0f, 14.0f, 10.0f);

            Assert.Greater(elevated.y, flat.y, "a positive elevation should lift the camera above the flat orbit");
        }

        [Test]
        public void PlaceOnOrbitIsCentredOnAnArbitraryFocus()
        {
            var focus = new Vector3(50.0f, 30.0f, -20.0f);
            Vector3 position = ClaimCutsceneMath.PlaceOnOrbit(focus, 0.0f, 0.0f, 0.0f, 10.0f);

            Assert.AreEqual(focus + new Vector3(10.0f, 0.0f, 0.0f), position);
        }

        [Test]
        public void StartAngleFacesTowardsWherethePlayerActuallyStands()
        {
            float alongX = ClaimCutsceneMath.StartAngle(Vector3.zero, new Vector3(5.0f, 0.0f, 0.0f));
            float alongZ = ClaimCutsceneMath.StartAngle(Vector3.zero, new Vector3(0.0f, 0.0f, 5.0f));

            Assert.AreEqual(0.0f, alongX, 0.001f);
            Assert.AreEqual(Mathf.PI * 0.5f, alongZ, 0.001f);
        }

        [Test]
        public void StartAngleDefaultsToZeroWhenThePlayerIsExactlyAtTheCentre()
        {
            float angle = ClaimCutsceneMath.StartAngle(new Vector3(3.0f, 0.0f, 4.0f), new Vector3(3.0f, 99.0f, 4.0f));

            Assert.AreEqual(0.0f, angle, "standing exactly on the centre column has no meaningful direction -- should not throw or produce NaN");
        }

        [Test]
        public void EaseStartsAtZeroEndsAtOneAndIsMonotonic()
        {
            Assert.AreEqual(0.0f, ClaimCutsceneMath.Ease(0.0f), 0.0001f);
            Assert.AreEqual(1.0f, ClaimCutsceneMath.Ease(1.0f), 0.0001f);
            Assert.AreEqual(0.5f, ClaimCutsceneMath.Ease(0.5f), 0.0001f, "smoothstep is symmetric around its midpoint");
            Assert.Less(ClaimCutsceneMath.Ease(0.25f), ClaimCutsceneMath.Ease(0.75f));
        }

        [Test]
        public void EaseClampsOutOfRangeInput()
        {
            Assert.AreEqual(0.0f, ClaimCutsceneMath.Ease(-1.0f));
            Assert.AreEqual(1.0f, ClaimCutsceneMath.Ease(2.0f));
        }
    }
}
