using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The bow's and the crossbow's rules (docs/ARCHERY.md): what the draw buys, what a bolt is, and the crossbow's profile.</summary>
    public sealed class ArcheryTests
    {
        [Test]
        public void TheDrawBuysRangeDamageAndSpeedUpToFull()
        {
            Assert.AreEqual(ArcheryRules.TapFlatRange, ArcheryRules.FlatRangeFor(0.0f), 1e-4f, "a tap is the least draw");
            Assert.AreEqual(ArcheryRules.TapFlatRange, ArcheryRules.FlatRangeFor(ArcheryRules.MinDraw), 1e-4f);
            Assert.AreEqual(ArcheryRules.FullFlatRange, ArcheryRules.FlatRangeFor(1.0f), 1e-4f);
            Assert.AreEqual(ArcheryRules.FullFlatRange, ArcheryRules.FlatRangeFor(3.0f), 1e-4f, "past full is full");
            Assert.Less(ArcheryRules.FlatRangeFor(0.5f), ArcheryRules.FullFlatRange);
            Assert.Greater(ArcheryRules.FlatRangeFor(0.5f), ArcheryRules.TapFlatRange);
            Assert.AreEqual(ArcheryRules.TapDamage, ArcheryRules.DamageFor(0.0f), 1e-4f);
            Assert.AreEqual(ArcheryRules.FullDamage, ArcheryRules.DamageFor(1.0f), 1e-4f);
            Assert.AreEqual(ArcheryRules.FullFallSpeed, ArcheryRules.FallSpeedFor(1.0f), 1e-4f);
            Assert.Greater(ArcheryRules.FallSpeedFor(1.0f), ArcheryRules.FallSpeedFor(0.2f), "a fuller draw flies flatter after the flat range");
        }

        [Test]
        public void ABoltHitsHarderAndFurtherThanAFullArrowAndTakesLongerToReload()
        {
            Assert.Greater(ArcheryRules.BoltDamage, ArcheryRules.FullDamage);
            Assert.Greater(ArcheryRules.BoltFlatRange, ArcheryRules.FullFlatRange);
            Assert.Greater(ArcheryRules.CrossbowReloadSeconds, ArcheryRules.FullDrawSeconds * 2.0f, "the crossbow pays for its damage in reload time");
            Assert.Greater(ArcheryRules.FullDamage, FirearmProfile.AK47().Damage, "a full arrow is worth more than an AK round for a second's draw");
        }

        [Test]
        public void TheCrossbowIsARifleWithOneBoltThatSpansItselfAndThrowsNoBrass()
        {
            FirearmProfile crossbow = FirearmProfile.Crossbow();
            Assert.IsTrue(crossbow.IsRifle, "it is held and aimed as a rifle");
            Assert.AreEqual(1, crossbow.MagazineSize);
            Assert.IsFalse(crossbow.Automatic);
            Assert.IsTrue(crossbow.ReloadsAfterEveryShot);
            Assert.AreEqual(ArcheryRules.CrossbowReloadSeconds, crossbow.EmptyReloadSeconds, 1e-4f, "every reload is from empty");
            Assert.AreEqual(0.0f, crossbow.CasingLength, "a crossbow ejects no casing");
            Assert.AreEqual(ArcheryRules.BoltFallSpeed, crossbow.ArrowFallSpeed, 1e-4f, "its shot is an arrow that falls past the range");
            Assert.AreEqual(ArcheryRules.BoltDamage, crossbow.Damage, 1e-4f);
            Assert.AreEqual(ArcheryRules.BoltFlatRange, crossbow.Range, 1e-4f);
            Assert.IsEmpty(crossbow.MotionResource, "no baked reload: a timer spans it");
            Assert.AreEqual(0.0f, FirearmProfile.AK47().ArrowFallSpeed, "a gun's shot is a bullet");
        }
    }
}
