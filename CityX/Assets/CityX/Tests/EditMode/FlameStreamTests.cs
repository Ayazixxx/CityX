using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The flame staff's rules (docs/STAFFS.md): what its stream reaches, what the rain does to it, and what it costs.</summary>
    public sealed class FlameStreamTests
    {
        [Test]
        public void TheStreamIsANarrowConeOfItsReach()
        {
            Vector3 from = new Vector3(0.0f, 2.0f, 0.0f);
            Assert.IsTrue(FlameStreamRules.InStream(from, Vector3.forward, from + Vector3.forward * 5.0f, FlameStreamRules.Range), "straight ahead");
            Assert.IsTrue(FlameStreamRules.InStream(from, Vector3.forward, from + new Vector3(0.6f, 0.0f, 5.0f), FlameStreamRules.Range), "a little to one side");
            Assert.IsFalse(FlameStreamRules.InStream(from, Vector3.forward, from + new Vector3(2.0f, 0.0f, 5.0f), FlameStreamRules.Range), "well to one side");
            Assert.IsFalse(FlameStreamRules.InStream(from, Vector3.forward, from + Vector3.forward * (FlameStreamRules.Range + 1.0f), FlameStreamRules.Range), "past its reach");
            Assert.IsFalse(FlameStreamRules.InStream(from, Vector3.forward, from + Vector3.back * 2.0f, FlameStreamRules.Range), "behind");
        }

        [Test]
        public void RainShortensItToHalfAtWorst()
        {
            Assert.AreEqual(FlameStreamRules.Range, FlameStreamRules.ReachFor(1.0f), 1e-4f, "dry");
            Assert.AreEqual(FlameStreamRules.Range * 0.5f, FlameStreamRules.ReachFor(0.0f), 1e-4f, "a downpour");
            Assert.Less(FlameStreamRules.ReachFor(0.5f), FlameStreamRules.Range);
        }

        [Test]
        public void ItCostsMoreManaThanComesBackAndReachesFurtherThanTheFlamethrower()
        {
            Assert.Greater(FlameStreamRules.ManaPerSecond, PlayerVitals.ManaRegenPerSecond, "it could be held for ever");
            Assert.Greater(FlameStreamRules.Range, Flamethrower.Range, "a staff throws its fire further than the flamethrower");
            Assert.Greater(FlameStreamRules.DamagePerSecond, Flamethrower.DamagePerSecond, "it pays mana, so it burns hotter");
            Assert.AreEqual(Flamethrower.ConeHalfAngleDegrees, FlameStreamRules.ConeHalfAngleDegrees, 1e-4f, "the same cone as the flamethrower's");
        }
    }
}
