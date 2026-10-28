using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The water staff's rules (docs/STAFFS.md): what the jet reaches, the throw it gives, and how its "3 damage consecutively" adds up.</summary>
    public sealed class WaterJetTests
    {
        [Test]
        public void TheJetIsANarrowLineOutToItsReach()
        {
            Vector3 from = new Vector3(0.0f, 1.8f, 0.0f);
            Assert.IsTrue(WaterJetRules.InStream(from, Vector3.forward, from + Vector3.forward * 6.0f), "on the line");
            Assert.IsTrue(WaterJetRules.InStream(from, Vector3.forward, from + Vector3.forward * 6.0f + Vector3.right * 0.4f), "within the stream's width");
            Assert.IsTrue(WaterJetRules.InStream(from, Vector3.forward, from + Vector3.forward * 15.0f + Vector3.right * 0.9f), "far out, within its angle");
            Assert.IsFalse(WaterJetRules.InStream(from, Vector3.forward, from + Vector3.forward * 6.0f + Vector3.right * 1.5f), "beside the line");
            Assert.IsFalse(WaterJetRules.InStream(from, Vector3.forward, from + Vector3.forward * (WaterJetRules.Range + 1.0f)), "past its reach");
            Assert.IsFalse(WaterJetRules.InStream(from, Vector3.forward, from - Vector3.forward * 2.0f), "behind the staff");
        }

        [Test]
        public void TheThrowGoesAlongTheJetAndLiftsALittle()
        {
            Vector3 thrown = WaterJetRules.KnockdownVelocity(new Vector3(0.0f, -0.3f, 1.0f).normalized);
            Assert.AreEqual(WaterJetRules.KnockdownSpeed, thrown.z, 1e-3f, "along the jet, flat");
            Assert.AreEqual(0.0f, thrown.x, 1e-3f);
            Assert.AreEqual(WaterJetRules.KnockdownLift, thrown.y, 1e-3f, "lifted off the ground, however the jet points down");
        }

        /// <summary>Water does not cut: its hits are struck in a bloodless scope, which closes behind them.</summary>
        [Test]
        public void AWaterHitDrawsNoBloodAndTheScopeCloses()
        {
            Assert.IsFalse(CityX.Character.DamageContext.Bloodless);
            using (CityX.Character.DamageContext.WithoutBlood())
            {
                Assert.IsTrue(CityX.Character.DamageContext.Bloodless);
            }
            Assert.IsFalse(CityX.Character.DamageContext.Bloodless, "the scope did not close");
        }

        /// <summary>Owner: "a bit of damage not much though just like 3 damage consecutively as you are spraying".</summary>
        [Test]
        public void ThreeDamageATickTenASecondWellUnderTheIceStaff()
        {
            Assert.AreEqual(3.0f, WaterJetRules.DamagePerTick);
            Assert.AreEqual(1, WaterJetRules.TicksIn(0.0f), "the first tick the moment the jet lands");
            Assert.AreEqual(4, WaterJetRules.TicksIn(1.0f));
            Assert.AreEqual(0, WaterJetRules.TicksIn(-1.0f));
            float aSecond = WaterJetRules.DamagePerTick / WaterJetRules.TickSeconds;
            float iceStaff = 8.0f / IceShardCaster.StaffShotSeconds;
            Assert.Less(aSecond, iceStaff * 0.5f, "the hose hurts much less than the ice staff");
            Assert.Greater(WaterJetRules.ManaPerSecond, 0.0f, "it costs mana, as every staff does");
        }
    }
}
