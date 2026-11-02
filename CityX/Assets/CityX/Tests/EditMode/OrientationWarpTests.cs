using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Orientation warping's choice (OrientationWarp): the nearest straight way shown, held against flicker, and the hips' turn toward the travel.</summary>
    public sealed class OrientationWarpTests
    {
        private static Vector2 At(float degrees, float speed = 4.0f) =>
            new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad)) * speed;

        [Test]
        public void ADiagonalIsShownAsTheWalkAndTheHipsTurnTheRest()
        {
            var warp = new OrientationWarp();
            Vector2 shown = warp.Shown(At(45.0f));
            Assert.AreEqual(0.0f, shown.x, 1e-3f, "a forward diagonal was not shown as the forward walk");
            Assert.AreEqual(4.0f, shown.y, 1e-3f, "the speed was not kept");
            Assert.AreEqual(45.0f, OrientationWarp.DegreesFor(shown, At(45.0f)), 1e-3f);
            Assert.AreEqual(-45.0f, OrientationWarp.DegreesFor(At(180.0f), At(135.0f)), 1e-3f, "backing off to the right turns the hips the other way");
        }

        [Test]
        public void TheShownWayHoldsPastTheHalfwayAndSwitchesBeyond()
        {
            var warp = new OrientationWarp();
            warp.Shown(At(0.0f));
            Assert.AreEqual(0.0f, warp.Shown(At(50.0f)).x, 1e-3f, "it switched to the side-step at 50 degrees and would flicker");
            Assert.AreEqual(4.0f, warp.Shown(At(60.0f)).x, 1e-3f, "it did not switch to the side-step at 60 degrees");
            Assert.AreEqual(4.0f, warp.Shown(At(40.0f)).x, 1e-3f, "coming back it left the side-step too early");
        }

        [Test]
        public void StandingOrStartingNothingIsWarpedAndTheTurnIsCapped()
        {
            Assert.AreEqual(0.0f, OrientationWarp.DegreesFor(At(0.0f, 0.2f), At(45.0f, 0.2f)));
            Assert.AreEqual(OrientationWarp.MaxDegrees, OrientationWarp.DegreesFor(At(0.0f), At(80.0f)), 1e-3f);
        }
    }
}
