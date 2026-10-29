using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Who stands in the Healing Aura's circle, with no scene (docs/HEALING.md).</summary>
    public sealed class HealingAuraRulesTests
    {
        [Test]
        public void ABodyStandsInTheCircleWithinItsRadiusAndAStepUpOrDown()
        {
            Vector3 centre = new Vector3(10.0f, 2.0f, -4.0f);
            Assert.IsTrue(HealingAuraRules.Inside(centre, centre + new Vector3(HealingAuraRules.Radius - 0.1f, 0.0f, 0.0f)));
            Assert.IsFalse(HealingAuraRules.Inside(centre, centre + new Vector3(HealingAuraRules.Radius * 0.8f, 0.0f, HealingAuraRules.Radius * 0.8f)),
                "a body past the rim stood in it");
            Assert.IsTrue(HealingAuraRules.Inside(centre, centre + new Vector3(1.0f, 1.0f, 0.0f)), "a body a step up was out of it");
            Assert.IsFalse(HealingAuraRules.Inside(centre, centre + new Vector3(1.0f, HealingAuraRules.HeightReach + 0.5f, 0.0f)),
                "a body a storey above stood in it");
        }

        [Test]
        public void AGiantsCircleIsAGiantsSize()
        {
            Vector3 feet = new Vector3(HealingAuraRules.Radius * 5.0f, 0.0f, 0.0f);
            Assert.IsFalse(HealingAuraRules.Inside(Vector3.zero, feet));
            Assert.IsTrue(HealingAuraRules.Inside(Vector3.zero, feet, 10.0f), "a giant's circle is no bigger than anyone's");
        }

        [Test]
        public void ItHealsSlowly()
        {
            Assert.Less(HealingAuraRules.HealPerSecond, 10.0f, "a slow heal");
            Assert.AreEqual(HealingAuraRules.HealPerSecond * HealingAuraRules.TickSeconds, HealingAuraRules.HealPerTick, 1e-4f);
            Assert.Greater(HealingAuraRules.CooldownSeconds, HealingAuraRules.DurationSeconds, "it could be kept up for ever");
        }

        /// <summary>The circle's rim is drawn at its radius and its glow fades out before the square it is painted on ends (a cut edge showed).</summary>
        [Test]
        public void TheRimIsAtTheRadiusAndNothingIsDrawnAtTheSquaresEdge()
        {
            float rimU = 0.5f + HealingAuraCircle.RimShare * 0.5f;
            Assert.Greater(HealingAuraCircle.RingAlpha(rimU, 0.5f), 0.95f, "the rim is not at the circle's radius");
            Assert.Greater(HealingAuraCircle.RingAlpha(0.5f, 0.5f), 0.05f, "the circle is not filled");
            foreach (Vector2 edge in new[] { new Vector2(0.0f, 0.5f), new Vector2(1.0f, 0.5f), new Vector2(0.5f, 0.0f), new Vector2(0.5f, 1.0f) })
            {
                Assert.Less(HealingAuraCircle.RingAlpha(edge.x, edge.y), 0.01f, "the rim's glow runs off the square at " + edge);
                Assert.Less(HealingAuraCircle.InnerAlpha(edge.x, edge.y), 0.01f, "the inner ring runs off the square at " + edge);
            }
        }
    }
}
