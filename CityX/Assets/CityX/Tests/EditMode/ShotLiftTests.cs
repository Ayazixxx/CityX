using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A weak slingshot shot lobbed onto the crosshair (ShotLift; owner, 2026-11-16: "make it better at going towards the crosshair during less
    /// charges shots"): the solved shot, flown as ArrowProjectile flies a pebble, comes down on the point.
    /// </summary>
    public sealed class ShotLiftTests
    {
        private static readonly Vector3 Eye = new Vector3(0.9f, 2.9f, -5.0f);

        /// <summary>Flies the shot (flat for the flat range, then falling on along the same line) to the target's distance across; its height there.</summary>
        private static float HeightAt(Vector3 direction, float flatRange, float fallSpeed, Vector3 target)
        {
            Vector3 position = Eye + direction * flatRange;
            Vector3 velocity = direction * fallSpeed;
            float before = Flat(target - position);
            for (int i = 0; i < 100000; i++)
            {
                const float step = 0.0005f;
                velocity += Vector3.down * (ArcheryRules.Gravity * step);
                position += velocity * step;
                float now = Flat(target - position);
                if (now > before)
                {
                    return position.y;
                }
                before = now;
            }
            return float.NaN;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        [Test]
        public void ATapLandsOnWhatTheCrosshairIsOnPastItsFlatRange([Values(14.0f, 18.0f, 22.0f)] float distance)
        {
            DrawnProfile sling = DrawnProfile.Slingshot;
            float flat = sling.FlatRangeFor(0.0f), speed = sling.FallSpeedFor(0.0f);
            var target = new Vector3(0.0f, 1.5f, distance);
            Vector3 aimed = (target - Eye).normalized;

            Assert.Less(HeightAt(aimed, flat, speed, target), target.y - 0.2f, "the shot did not fall short as aimed: nothing to fix");
            Assert.IsTrue(ShotLift.Solve(Eye, aimed, flat, speed, target, out Vector3 lifted), "no lob found for a point in reach");
            Assert.Greater(lifted.y, aimed.y, "lobbed down");
            Assert.AreEqual(target.y, HeightAt(lifted, flat, speed, target), 0.1f, "the lob does not come down on the crosshair");
        }

        [Test]
        public void OutOfReachItGoesAsAimed()
        {
            DrawnProfile sling = DrawnProfile.Slingshot;
            var target = new Vector3(0.0f, 1.5f, 55.0f);
            Vector3 aimed = (target - Eye).normalized;
            Assert.IsFalse(ShotLift.Solve(Eye, aimed, sling.FlatRangeFor(0.0f), sling.FallSpeedFor(0.0f), target, out Vector3 lifted));
            Assert.AreEqual(aimed, lifted);
        }
    }
}
