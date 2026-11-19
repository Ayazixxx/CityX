using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The rules of each kind of shield (docs/SHIELD.md; the riot shield's are the owner's of 2026-11-18).</summary>
    public sealed class ShieldTraitsTests
    {
        [Test]
        public void TheRiotShieldGoesBesideBareHandsAOneHandedWeaponAPistolOrAWandOnly()
        {
            ShieldTraits riot = ShieldTraits.Riot;
            Assert.IsTrue(riot.GoesBeside(HeldKind.BareHands));
            Assert.IsTrue(riot.GoesBeside(HeldKind.OneHandedMelee));
            Assert.IsTrue(riot.GoesBeside(HeldKind.Pistol), "owner: \"so you can equip shield and pistol\"");
            Assert.IsTrue(riot.GoesBeside(HeldKind.Wand), "owner: \"I think wands only\"");
            Assert.IsFalse(riot.GoesBeside(HeldKind.Staff));
            Assert.IsFalse(riot.GoesBeside(HeldKind.TwoHandedMelee));
            Assert.IsFalse(riot.GoesBeside(HeldKind.LongGun));
            Assert.IsFalse(riot.GoesBeside(HeldKind.SprayCan));
            Assert.IsFalse(riot.GoesBeside(HeldKind.Other));
        }

        [Test]
        public void TheRoundShieldGoesBesideAnythingButAGunOrASprayCanAsItAlwaysDid()
        {
            ShieldTraits round = ShieldTraits.Round;
            Assert.IsTrue(round.GoesBeside(HeldKind.BareHands));
            Assert.IsTrue(round.GoesBeside(HeldKind.OneHandedMelee));
            Assert.IsTrue(round.GoesBeside(HeldKind.TwoHandedMelee));
            Assert.IsTrue(round.GoesBeside(HeldKind.Staff));
            Assert.IsTrue(round.GoesBeside(HeldKind.Wand));
            Assert.IsTrue(round.GoesBeside(HeldKind.Other));
            Assert.IsFalse(round.GoesBeside(HeldKind.Pistol));
            Assert.IsFalse(round.GoesBeside(HeldKind.LongGun));
            Assert.IsFalse(round.GoesBeside(HeldKind.SprayCan));
        }

        [Test]
        public void TheRiotShieldLeavesTheFeetOfAStandingBodyShowingAndCoversACrouchedOne()
        {
            ShieldTraits riot = ShieldTraits.Riot;
            Assert.IsFalse(riot.CoversHeight(0.2f, 1.0f, false), "a standing body's feet are behind the shield");
            Assert.IsTrue(riot.CoversHeight(1.5f, 1.0f, false), "the chest is not behind the shield");
            Assert.IsTrue(riot.CoversHeight(2.5f, 1.0f, false), "the head is not behind the shield");
            Assert.IsTrue(riot.CoversHeight(0.2f, 1.0f, true), "crouched, the feet still show");
            Assert.IsFalse(riot.CoversHeight(0.9f, 2.0f, false), "a body twice the size shows no more of its legs");
            Assert.IsTrue(ShieldTraits.Round.CoversHeight(0.0f, 1.0f, false), "the round shield's cover changed");
        }

        [Test]
        public void TheRiotShieldOpensToAttackAndDoesNotSlowTheBodyAndTheRoundOneIsAsItWas()
        {
            Assert.Greater(ShieldTraits.Riot.OpenToAttackSeconds, 0.0f);
            Assert.AreEqual(1.0f, ShieldTraits.Riot.MoveSpeedShare, "owner, 2026-11-06: no annoying penalties");
            Assert.AreEqual(0.0f, ShieldTraits.Round.OpenToAttackSeconds);
            Assert.AreEqual(PlayerLocomotion.ShieldMoveSpeedShare, ShieldTraits.Round.MoveSpeedShare);
        }
    }
}
