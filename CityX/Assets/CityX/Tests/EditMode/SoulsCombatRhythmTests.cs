using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE RHYTHM OF A SWORD FIGHT (owner, 2026-10-17: "if there wasnt dashing it would pretty much be two people spamming click on eachother"):
    /// a landed light holds the victim until the chain's next cut arrives, so a hit earns its follow-up and the fight is turns, not a race of
    /// clicks. Pure arithmetic on the sword's own timings: the gap from one cut's contact to the next, against the hitstun and the freezes.
    /// </summary>
    public sealed class SoulsCombatRhythmTests
    {
        private GameObject _object;
        private SoulsSwordCombat _combat;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Combat");
            _combat = _object.AddComponent<SoulsSwordCombat>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        /// <summary>Seconds from one light's contact (the start of its live window) to the next light's contact, chained at the first window.</summary>
        private static float ContactToContact(SoulsAttack first, SoulsAttack second)
        {
            float afterContact = first.Active + first.Recovery * first.ChainAt;
            return afterContact + second.Startup;
        }

        /// <summary>
        /// Seconds the victim is free for before the next cut lands: the gap, plus the attacker's own hit-stop (both stand still through it),
        /// less what holds the victim (its stun and its own instant of freeze). Enough to raise a guard, never enough to swing first.
        /// </summary>
        private float FreeBeforeTheNextCut(SoulsAttack first, SoulsAttack second)
        {
            float attackerStop = _combat.HitStopSeconds;
            float victimStop = first.HitstunSeconds >= HitImpactEffects.HeavyHitstunSeconds ? HitImpactEffects.HeavyStopSeconds : HitImpactEffects.LightStopSeconds;
            return ContactToContact(first, second) + attackerStop - (first.HitstunSeconds + victimStop);
        }

        [Test]
        public void ALandedOneHandedLightHoldsTheVictimUntilTheNextCutLands()
        {
            float free = FreeBeforeTheNextCut(_combat.OneHandLight1, _combat.OneHandLight2);
            Assert.GreaterOrEqual(free, 0.0f, "the second cut lands while the first still holds: a stun-lock, not a follow-up");
            Assert.LessOrEqual(free, 0.12f, $"the victim is free for {free:0.00} s before the second cut: long enough to swing first again");
            Assert.Greater(_combat.OneHandLight2.Startup, free + 0.1f, "a swing started the moment the stun ends must not land before the follow-up");
            // The one-handed chain goes back and forth (slash, cut, slash...): the cut's follow-up is the slash again.
            float freeBack = FreeBeforeTheNextCut(_combat.OneHandLight2, _combat.OneHandLight1);
            Assert.GreaterOrEqual(freeBack, 0.0f);
            Assert.LessOrEqual(freeBack, 0.12f, $"free for {freeBack:0.00} s between the cut and the slash after it");
        }

        [Test]
        public void ALandedTwoHandedLightHoldsTheVictimUntilTheNextCutLands()
        {
            float free1 = FreeBeforeTheNextCut(_combat.Light1, _combat.Light2);
            float free2 = FreeBeforeTheNextCut(_combat.Light2, _combat.Light3);
            Assert.GreaterOrEqual(free1, 0.0f);
            Assert.LessOrEqual(free1, 0.12f, $"free for {free1:0.00} s between the forehand and the backhand");
            Assert.GreaterOrEqual(free2, 0.0f);
            Assert.LessOrEqual(free2, 0.12f, $"free for {free2:0.00} s between the backhand and the finisher");
        }

        [Test]
        public void TheHeaviesReadAsHeavyToTheBodyTheyLandOn()
        {
            Assert.GreaterOrEqual(_combat.Heavy.HitstunSeconds, HitImpactEffects.HeavyHitstunSeconds, "the two-handed heavy's stun is too short to count as heavy");
            Assert.GreaterOrEqual(_combat.OneHandHeavy.HitstunSeconds, HitImpactEffects.HeavyHitstunSeconds, "the one-handed heavy's stun is too short to count as heavy");
            Assert.Less(_combat.OneHandLight1.HitstunSeconds, HitImpactEffects.HeavyHitstunSeconds, "a light must not count as heavy");
        }

        [Test]
        public void TheHitStopIsAFewFramesNotAPause()
        {
            // Street Fighter V holds 8-15 frames; past about 0.12 s a freeze reads as a dropped frame (the game-feel research), and the heavy
            // may run a little past it for its weight.
            Assert.That(_combat.HitStopSeconds, Is.InRange(0.06f, 0.12f));
            Assert.That(_combat.HitStopSeconds * _combat.HeavyHitStopMultiplier, Is.InRange(0.1f, 0.2f));
            Assert.LessOrEqual(_combat.HitStopSeconds * _combat.HeavyHitStopMultiplier, PlayerLocomotion.MaxImpactFreezeSeconds,
                "the heavy's hit-stop would be cut short by the body's own freeze cap");
        }

        [Test]
        public void ALightLandsInAThirdOfASecondAndTheChainRunsQuick()
        {
            // Owner, 2026-10-23: "light attacks feel snappy; heavy ones feel powerful". A light's click lands in about a third of a second (the
            // research: a long anticipation on the player's own attack reads as input lag) and a chain's cuts come about every 0.6 s, hit-stop
            // included; the heavies take over half a second to land, so they read as heavy beside them.
            Assert.LessOrEqual(_combat.OneHandLight1.Startup + _combat.OneHandLight1.Active * 0.5f, 0.32f, "the one-handed slash is slow to land");
            Assert.LessOrEqual(_combat.Light1.Startup + _combat.Light1.Active * 0.5f, 0.36f, "the two-handed sweep is slow to land");
            Assert.LessOrEqual(ContactToContact(_combat.OneHandLight1, _combat.OneHandLight2) + _combat.HitStopSeconds, 0.6f, "the one-handed chain drags");
            Assert.LessOrEqual(ContactToContact(_combat.Light1, _combat.Light2) + _combat.HitStopSeconds, 0.65f, "the two-handed chain drags");
            Assert.GreaterOrEqual(_combat.Heavy.Startup, 0.55f, "the slam lands too soon to read as heavy");
            Assert.GreaterOrEqual(_combat.OneHandHeavy.Startup, 0.5f, "the overhead lands too soon to read as heavy");
        }
    }
}
