using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The Frost Staff's spray and chill, with no scene (docs/FROST.md).</summary>
    public sealed class FrostRulesTests
    {
        [Test]
        public void TheSprayIsAShortConeAlongTheCrosshair()
        {
            Vector3 from = Vector3.zero;
            Assert.IsTrue(FrostRules.InCone(from, Vector3.forward, new Vector3(0.0f, 0.0f, 6.0f)));
            Assert.IsTrue(FrostRules.InCone(from, Vector3.forward, new Vector3(0.8f, 0.0f, 6.0f)), "a body a little off the line was missed");
            Assert.IsFalse(FrostRules.InCone(from, Vector3.forward, new Vector3(4.0f, 0.0f, 6.0f)), "a body well off the line was sprayed");
            Assert.IsFalse(FrostRules.InCone(from, Vector3.forward, new Vector3(0.0f, 0.0f, FrostRules.Range + 1.0f)), "a body past its reach was sprayed");
        }

        [Test]
        public void TheColderTheSlowerAndTwoSecondsInItFreezes()
        {
            Assert.AreEqual(1.0f, FrostRules.SpeedShare(0.0f), 1e-4f);
            Assert.AreEqual(1.0f - FrostRules.MaxSlow, FrostRules.SpeedShare(1.0f), 1e-4f);
            Assert.Less(FrostRules.SpeedShare(0.6f), FrostRules.SpeedShare(0.3f), "colder is not slower");
            Assert.AreEqual(2.0f, FrostRules.TickSeconds / FrostRules.ChillPerTick, 1e-4f, "a full chill should take two seconds in the spray");
            Assert.Less(FrostRules.DamagePerSecond, 20.0f, "the spray should do only a little damage");
        }

        /// <summary>Each saved body is written by its own builder: one not rebuilt since Chill came in could never be frozen.</summary>
        [Test]
        public void EveryBodyCanBeChilled()
        {
            foreach (string body in new[] { "Player", "Civilian", "Swordsman", "Shoota", "Giant" })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Character/{body}.prefab");
                Assert.IsNotNull(prefab, $"no {body}.prefab");
                Assert.IsNotNull(prefab.GetComponent<Chill>(), $"{body}.prefab cannot be chilled -- rebuild it");
            }
        }
    }
}
