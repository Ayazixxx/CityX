using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The beam staffs' ticks and choices, with no scene (docs/STAFFS.md): the Arcane Beam Staff's burn and the Staff of Healing's tie.</summary>
    public sealed class BeamStaffRulesTests
    {
        [Test]
        public void TheFirstTickLandsAtOnceThenOneEveryTickHoweverTheFramesFall()
        {
            float since = ArcaneBeamRules.TickSeconds;
            Assert.AreEqual(1, SpellTicks.Due(ref since, 0.0f, ArcaneBeamRules.TickSeconds), "the first tick did not land at once");
            int ticks = 0;
            for (int i = 0; i < 60; i++)
            {
                ticks += SpellTicks.Due(ref since, 1.0f / 60.0f, ArcaneBeamRules.TickSeconds);
            }
            Assert.AreEqual(5, ticks, "a second of frames is five ticks");
            Assert.AreEqual(3, SpellTicks.Due(ref since, 0.6f, ArcaneBeamRules.TickSeconds), "a long frame owes every tick in it");
        }

        [Test]
        public void TheArcaneBeamIsTheLongReachOfTheStaffsAndPaysForItsBurn()
        {
            Assert.Greater(ArcaneBeamRules.Range, WaterJetRules.Range, "the beam should reach further than the water jet");
            Assert.AreEqual(ArcaneBeamRules.DamagePerSecond * ArcaneBeamRules.TickSeconds, ArcaneBeamRules.DamagePerTick, 1e-4f);
            Assert.Greater(ArcaneBeamRules.ManaPerSecond, CityX.Character.PlayerVitals.ManaRegenPerSecond, "held, the beam must cost more than mana comes back");
        }

        [Test]
        public void TheHealingBeamTiesToTheAllyNearestTheCrosshairWithinReach()
        {
            Vector3 eye = new Vector3(0.0f, 2.4f, -4.0f);
            Vector3 healer = Vector3.zero;
            float ahead = HealingBeamRules.LockScore(eye, Vector3.forward, healer, new Vector3(0.3f, 2.0f, 10.0f));
            float aside = HealingBeamRules.LockScore(eye, Vector3.forward, healer, new Vector3(2.0f, 2.0f, 10.0f));
            Assert.Less(ahead, aside, "the ally nearer the crosshair does not win");
            Assert.AreEqual(float.MaxValue, HealingBeamRules.LockScore(eye, Vector3.forward, healer, new Vector3(8.0f, 2.0f, 6.0f)), "an ally far off the crosshair was picked");
            Assert.AreEqual(float.MaxValue, HealingBeamRules.LockScore(eye, Vector3.forward, healer, new Vector3(0.0f, 2.0f, HealingBeamRules.LockRange + 2.0f)),
                "an ally out of reach was picked");
        }

        [Test]
        public void TheHealingBeamHoldsUntilPastItsStretchOrOutOfSightTooLong()
        {
            Assert.IsTrue(HealingBeamRules.Holds(HealingBeamRules.LockRange + 0.5f, 0.0f), "the beam let go just past the lock range");
            Assert.IsFalse(HealingBeamRules.Holds(HealingBeamRules.BreakRange + 0.1f, 0.0f), "the beam held past its stretch");
            Assert.IsTrue(HealingBeamRules.Holds(5.0f, 1.0f), "a second out of sight let go");
            Assert.IsFalse(HealingBeamRules.Holds(5.0f, HealingBeamRules.SightGraceSeconds + 0.1f), "the beam held on someone out of sight too long");
        }
    }
}
