using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The frag grenade's numbers, with no scene (docs/GRENADE.md).</summary>
    public sealed class GrenadeRulesTests
    {
        [Test]
        public void ItKillsAtItsMiddleStingsAtItsRimAndNothingPast()
        {
            Assert.GreaterOrEqual(GrenadeRules.Damage * GrenadeRules.DamageShare(0.0f), 100.0f, "a grenade at your feet does not kill");
            Assert.AreEqual(GrenadeRules.EdgeShare, GrenadeRules.DamageShare(GrenadeRules.Radius), 1e-4f);
            Assert.Greater(GrenadeRules.DamageShare(GrenadeRules.Radius * 0.5f), GrenadeRules.DamageShare(GrenadeRules.Radius));
            Assert.AreEqual(0.0f, GrenadeRules.DamageShare(GrenadeRules.Radius + 0.1f), 1e-6f);
        }

        [Test]
        public void ItsFuseLightBlinksFasterAsTheFuseBurns()
        {
            Assert.AreEqual(GrenadeRules.BlinkSlowestHz, GrenadeRules.BlinkHz(GrenadeRules.FuseSeconds), 1e-4f);
            Assert.AreEqual(GrenadeRules.BlinkFastestHz, GrenadeRules.BlinkHz(0.0f), 1e-4f);
            Assert.Greater(GrenadeRules.BlinkHz(0.5f), GrenadeRules.BlinkHz(1.5f));
        }

        [Test]
        public void ItIsLobbedAlongTheAimAtItsSpeed()
        {
            Vector3 aim = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 velocity = GrenadeRules.ThrowVelocity(aim);
            Assert.AreEqual(GrenadeRules.ThrowSpeed, velocity.magnitude, 1e-3f);
            Assert.Greater(velocity.y, 0.0f, "a level aim was not lifted into a lob");
            Assert.AreEqual(GrenadeRules.ThrowLiftDegrees, Vector3.Angle(aim, velocity), 0.1f);
            Assert.AreEqual(GrenadeRules.ThrowSpeed, GrenadeRules.ThrowVelocity(Vector3.up).magnitude, 1e-3f, "straight up broke the throw");
        }

        /// <summary>Owner, 2026-10-29: "when i look forward and throw it , it goes behind me".</summary>
        [Test]
        public void ItIsThrownWhereTheCameraLooksNotBehind()
        {
            var camera = new GameObject("Camera").transform;
            try
            {
                camera.rotation = Quaternion.Euler(10.0f, 70.0f, 0.0f);
                Vector3 thrown = GrenadeRules.ThrowVelocity(CityX.Character.PlayerInputReader.ThrowDirection(camera));
                Assert.Greater(Vector3.Dot(thrown.normalized, camera.forward), 0.9f, "the grenade does not go where the camera looks");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
