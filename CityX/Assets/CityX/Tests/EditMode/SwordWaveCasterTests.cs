using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    public sealed class SwordWaveCasterTests
    {
        [Test]
        public void ACastIsRefusedUntilItsCooldownAndReleasesAfterTheSwingDelay()
        {
            var player = new GameObject("Player");
            try
            {
                SwordWaveCaster caster = player.AddComponent<SwordWaveCaster>();
                Assert.IsTrue(caster.CanCast);
                Assert.IsTrue(caster.TryCast(1.2f, Vector3.forward, null));
                Assert.IsFalse(caster.CanCast, "busy swinging and on cooldown");
                Assert.IsFalse(caster.TryCast(1.2f, Vector3.forward, null));

                caster.Tick(caster.ReleaseDelaySeconds + 0.01f);   // the crescent has left (no prefab here, so nothing spawns)
                Assert.IsFalse(caster.CanCast, "still on cooldown");
                caster.Tick(caster.CooldownSeconds);
                Assert.IsTrue(caster.CanCast);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ADirectionWithNoLengthIsNotACast()
        {
            var player = new GameObject("Player");
            try
            {
                SwordWaveCaster caster = player.AddComponent<SwordWaveCaster>();
                Assert.IsFalse(caster.TryCast(1.2f, Vector3.zero, null));
                Assert.IsTrue(caster.CanCast, "nothing was spent");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
