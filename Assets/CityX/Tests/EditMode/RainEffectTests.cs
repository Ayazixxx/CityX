using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The pure math behind "make it look reallllly good" -- the storm ramp,
    /// the lightning flash curve, and the wet-material blend. All three are
    /// plain static functions specifically so they are checkable without a
    /// real scene, the same reasoning Jetpack.TiltTarget/EaseTilt and
    /// SkyCycle's own pure functions are split out for.
    /// </summary>
    public sealed class RainEffectTests
    {
        [Test]
        public void StormRampReachesFullStrengthGivenEnoughTime()
        {
            float strength = 0.0f;
            for (int i = 0; i < 600; i++)
            {
                strength = RainEffect.StormRamp(strength, 1.0f, 1.0f / 60.0f);
            }
            Assert.AreEqual(1.0f, strength, 0.001f);
        }

        [Test]
        public void StormRampNeverOvershootsItsTarget()
        {
            // A single huge deltaTime (a hitch, or a test with a coarse step)
            // must clamp at the target rather than sailing past it the way a
            // naive `current + rate * deltaTime` would.
            float strength = RainEffect.StormRamp(0.0f, 1.0f, 100.0f);
            Assert.AreEqual(1.0f, strength, 0.0001f);
        }

        [Test]
        public void StormRampEasesBackToZeroOnceClear()
        {
            float strength = 1.0f;
            for (int i = 0; i < 600; i++)
            {
                strength = RainEffect.StormRamp(strength, 0.0f, 1.0f / 60.0f);
            }
            Assert.AreEqual(0.0f, strength, 0.001f);
        }

        [Test]
        public void StormRampTakesRoughlyItsOwnDurationToArrive()
        {
            // Not yet fully there just BEFORE StormRampSeconds has elapsed --
            // pins the ramp to its own documented duration rather than some
            // other rate entirely.
            float strength = 0.0f;
            int framesJustShort = Mathf.FloorToInt((RainEffect.StormRampSeconds - 0.2f) * 60.0f);
            for (int i = 0; i < framesJustShort; i++)
            {
                strength = RainEffect.StormRamp(strength, 1.0f, 1.0f / 60.0f);
            }
            Assert.Less(strength, 1.0f, "the ramp finished suspiciously early");
        }

        [Test]
        public void LightningBoostIsFullyBrightThroughoutTheInitialHold()
        {
            float total = RainEffect.LightningFlashSeconds + RainEffect.LightningDecaySeconds;
            Assert.AreEqual(1.0f, RainEffect.LightningBoost(total, RainEffect.LightningFlashSeconds, RainEffect.LightningDecaySeconds), 0.0001f);
            Assert.AreEqual(1.0f, RainEffect.LightningBoost(RainEffect.LightningDecaySeconds + 0.001f, RainEffect.LightningFlashSeconds, RainEffect.LightningDecaySeconds), 0.0001f);
        }

        [Test]
        public void LightningBoostDecaysLinearlyToZero()
        {
            float halfDecay = RainEffect.LightningDecaySeconds * 0.5f;
            Assert.AreEqual(0.5f, RainEffect.LightningBoost(halfDecay, RainEffect.LightningFlashSeconds, RainEffect.LightningDecaySeconds), 0.001f);
            Assert.AreEqual(0.0f, RainEffect.LightningBoost(0.0f, RainEffect.LightningFlashSeconds, RainEffect.LightningDecaySeconds), 0.0001f);
        }

        [Test]
        public void LightningBoostIsZeroOnceThePastCountdownGoesNegative()
        {
            Assert.AreEqual(0.0f, RainEffect.LightningBoost(-1.0f, RainEffect.LightningFlashSeconds, RainEffect.LightningDecaySeconds));
        }

        [Test]
        public void WetColourIsUnchangedAtZeroWetness()
        {
            Color original = new Color(0.6f, 0.5f, 0.4f, 1.0f);
            Color result = RainEffect.WetColour(original, 0.0f);
            Assert.AreEqual(original.r, result.r, 0.0001f);
            Assert.AreEqual(original.g, result.g, 0.0001f);
            Assert.AreEqual(original.b, result.b, 0.0001f);
            Assert.AreEqual(original.a, result.a, 0.0001f);
        }

        [Test]
        public void WetColourDarkensRgbButLeavesAlphaAloneAtFullWetness()
        {
            Color original = new Color(0.6f, 0.5f, 0.4f, 0.73f);
            Color result = RainEffect.WetColour(original, 1.0f);
            Assert.AreEqual(original.r * RainEffect.WetColourMultiplier, result.r, 0.0001f);
            Assert.AreEqual(original.g * RainEffect.WetColourMultiplier, result.g, 0.0001f);
            Assert.AreEqual(original.b * RainEffect.WetColourMultiplier, result.b, 0.0001f);
            Assert.AreEqual(original.a, result.a, 0.0001f,
                "alpha should never be touched by the wet-ground darkening, only RGB");
        }

        [Test]
        public void WetSmoothnessRisesToTheWetValueAtFullWetness()
        {
            Assert.AreEqual(0.05f, RainEffect.WetSmoothnessFor(0.05f, 0.0f), 0.0001f);
            Assert.AreEqual(RainEffect.WetSmoothness, RainEffect.WetSmoothnessFor(0.05f, 1.0f), 0.0001f);
        }
    }
}
