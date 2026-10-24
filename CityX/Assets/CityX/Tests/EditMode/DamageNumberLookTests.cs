using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>How a damage number looks (owner, 2026-10-24: Fortnite's numbers, "more red based on the amount of the enemies healthbar you are taking away with that 1 attack").</summary>
    public sealed class DamageNumberLookTests
    {
        [Test]
        public void AScratchIsWhiteHalfTheBarIsFullRedAndRedderAllTheWay()
        {
            Assert.AreEqual(Color.white, DamageNumberLook.ColourFor(0.0f));
            Assert.AreEqual(Color.white, DamageNumberLook.ColourFor(DamageNumberLook.WhiteBelowShare));
            Assert.AreEqual(DamageNumberLook.Red, DamageNumberLook.ColourFor(DamageNumberLook.FullRedShare));
            Assert.AreEqual(DamageNumberLook.Red, DamageNumberLook.ColourFor(1.0f));
            float previous = -1.0f;
            for (float share = 0.0f; share <= 1.0f; share += 0.01f)
            {
                float redness = DamageNumberLook.Redness(share);
                Assert.GreaterOrEqual(redness, previous, $"a bigger bite at {share:0.00} of the bar is less red");
                previous = redness;
            }
        }

        [Test]
        public void AgainstAWeakerOpponentTheNumbersAreRedAgainstAStrongerOneWhite()
        {
            // The same blow of 25: a 50-health opponent loses half the bar, a 100-health one a quarter, a 1000-health one a fortieth.
            Assert.Greater(DamageNumberLook.Redness(25.0f / 50.0f), 0.95f, "half the bar is not red");
            Assert.That(DamageNumberLook.Redness(25.0f / 100.0f), Is.InRange(0.3f, 0.7f), "a quarter of the bar is not halfway red");
            Assert.Less(DamageNumberLook.Redness(25.0f / 1000.0f), 0.01f, "a fortieth of the bar is not white");
        }

        [Test]
        public void ABiggerBiteIsBiggerAndAHeavyBlowBiggerStill()
        {
            Assert.AreEqual(DamageNumberLook.SmallSize, DamageNumberLook.SizeFor(0.0f, false), 0.01f);
            Assert.AreEqual(DamageNumberLook.BigSize, DamageNumberLook.SizeFor(1.0f, false), 0.01f);
            Assert.Greater(DamageNumberLook.SizeFor(0.2f, true), DamageNumberLook.SizeFor(0.2f, false));
        }

        [Test]
        public void ItPopsRisesDriftsAndFadesWithinItsLife()
        {
            Assert.AreEqual(DamageNumberLook.PopScale, DamageNumberLook.ScaleAt(0.0f), 1e-4f);
            Assert.AreEqual(1.0f, DamageNumberLook.ScaleAt(DamageNumberLook.PopSeconds), 1e-4f);
            Assert.AreEqual(Vector2.zero, DamageNumberLook.OffsetAt(0.0f, 1.0f));
            Vector2 end = DamageNumberLook.OffsetAt(DamageNumberLook.LifeSeconds, -1.0f);
            Assert.AreEqual(DamageNumberLook.RisePixels, end.y, 1e-3f);
            Assert.AreEqual(-DamageNumberLook.DriftPixels, end.x, 1e-3f);
            Assert.AreEqual(1.0f, DamageNumberLook.AlphaAt(DamageNumberLook.FadeFromSeconds), 1e-4f);
            Assert.AreEqual(0.0f, DamageNumberLook.AlphaAt(DamageNumberLook.LifeSeconds), 1e-4f);
            Assert.LessOrEqual(DamageNumberLook.LifeSeconds, 1.0f, "a number that lingers past a second clutters the screen");
        }

        [Test]
        public void FarHitsAreSmallerAndBeyondReachNotShown()
        {
            Assert.AreEqual(1.0f, DamageNumberLook.DistanceScale(3.0f), 1e-4f);
            Assert.Less(DamageNumberLook.DistanceScale(40.0f), 1.0f);
            Assert.AreEqual(0.0f, DamageNumberLook.DistanceScale(DamageNumberLook.MaxMetres + 1.0f));
        }

        [Test]
        public void TheNumberIsWholePointsAndNeverZero()
        {
            Assert.AreEqual("23", DamageNumberLook.TextFor(22.6f));
            Assert.AreEqual("1", DamageNumberLook.TextFor(0.2f));
        }
    }
}
