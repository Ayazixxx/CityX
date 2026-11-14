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
        }

        /// <summary>The first two cuts are guaranteed, the finisher is not: its bigger wind-up leaves the target time to dash or raise a guard.</summary>
        [Test]
        public void TheOneHandedFinisherCanBeGotOutOfAfterTheSecondCut()
        {
            float free = FreeBeforeTheNextCut(_combat.OneHandLight2, _combat.OneHandLight3);
            Assert.Greater(free, 0.15f, $"the second cut's stun runs almost to the finisher ({free:0.00} s free): three hits are guaranteed");
            Assert.Less(free, _combat.OneHandLight3.Startup, $"free for {free:0.00} s: long enough to swing and land first every time");
        }

        /// <summary>
        /// Owner, 2026-11-04: "if the final hit gives a stagger then why wouldnt you just keep attacking you can just spam click". The target is free before the
        /// attacker can act again, at the latest moment the finisher can land (the end of its live window) and with the highest Melee Speed that counts.
        /// </summary>
        [TestCase(1.0f)]
        [TestCase(1.5f)]
        public void TheFinisherFreesTheTargetBeforeTheAttackerCanAct(float meleeSpeed)
        {
            SoulsAttack finisher = _combat.OneHandLight3;
            Assert.AreEqual(1.0f, finisher.ChainAt, 0.001f, "the finisher's recovery can be cancelled into the next swing");
            float targetHeld = finisher.HitstunAt(meleeSpeed) + HitImpactEffects.HeavyStopSeconds;
            float attackerHeld = finisher.Recovery / meleeSpeed + _combat.HitStopSeconds * _combat.HeavyHitStopMultiplier;
            Assert.Greater(attackerHeld - targetHeld, 0.15f, $"the target is held {targetHeld:0.00} s and the attacker {attackerHeld:0.00} s: the combo can be looped");
            SoulsAttack next = _combat.OneHandLight1;
            Assert.Greater(attackerHeld + next.Startup / meleeSpeed - targetHeld, next.Startup / meleeSpeed, "the target is not free before the next swing starts");
        }

        [Test]
        public void TheFinisherIsTheBiggestOfTheThreeAndCostsTheMost()
        {
            SoulsAttack finisher = _combat.OneHandLight3;
            Assert.Greater(finisher.DamageScale, _combat.OneHandLight2.DamageScale);
            Assert.Greater(finisher.StaminaCost, _combat.OneHandLight2.StaminaCost);
            Assert.Greater(finisher.LungeMetres, _combat.OneHandLight2.LungeMetres);
            Assert.IsTrue(finisher.LandsHeavily, "the finisher does not land as a heavy blow");
            Assert.Less(finisher.HitstunSeconds, HitImpactEffects.HeavyHitstunSeconds, "the finisher's stagger is as long as a heavy's");
            Assert.IsFalse(finisher.BreaksGuard, "the finisher breaks guards: blocking it must work");
            Assert.IsFalse(_combat.OneHandLight1.LandsHeavily, "a first cut lands as a heavy blow");
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
            // The sledgehammer's smash in the finisher's place (owner, 2026-11-05): the backhand still holds the victim until it lands.
            float free3 = FreeBeforeTheNextCut(_combat.Light2, _combat.HammerLight3);
            Assert.GreaterOrEqual(free3, 0.0f, "the backhand lets go before the hammer's smash can land");
            Assert.LessOrEqual(free3, 0.12f, $"free for {free3:0.00} s between the backhand and the hammer's smash");
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
        public void ALightCanBeSeenComingAndStillLandsInUnderHalfASecond()
        {
            // Owner, 2026-11-13: swings "telegraphed ... akin to dark souls combat", and slower, readable wind-ups chosen over the third of a
            // second a light took to land. A light's wind-up is long enough to be seen (the blade live no sooner than 0.3 s after the click)
            // and it still lands inside half a second; a chain's cuts come about every 0.7 s, hit-stop included; the heavies take well over
            // half a second, so they read as heavy beside them and can be answered.
            Assert.GreaterOrEqual(_combat.OneHandLight1.Startup, 0.3f, "the one-handed slash is live before its wind-up can be read");
            Assert.GreaterOrEqual(_combat.Light1.Startup, 0.34f, "the two-handed sweep is live before its wind-up can be read");
            Assert.LessOrEqual(_combat.OneHandLight1.Startup + _combat.OneHandLight1.Active * 0.5f, 0.42f, "the one-handed slash is slow to land");
            Assert.LessOrEqual(_combat.Light1.Startup + _combat.Light1.Active * 0.5f, 0.5f, "the two-handed sweep is slow to land");
            Assert.LessOrEqual(ContactToContact(_combat.OneHandLight1, _combat.OneHandLight2) + _combat.HitStopSeconds, 0.7f, "the one-handed chain drags");
            Assert.LessOrEqual(ContactToContact(_combat.Light1, _combat.Light2) + _combat.HitStopSeconds, 0.8f, "the two-handed chain drags");
            Assert.GreaterOrEqual(_combat.Heavy.Startup, 0.75f, "the slam lands too soon to read as heavy");
            Assert.GreaterOrEqual(_combat.OneHandHeavy.Startup, 0.6f, "the overhead lands too soon to read as heavy");
        }

        [Test]
        public void AStandingBodyIsSteppedIntoItsCutAndARunningAttackRunsOnItsOwnLegs()
        {
            // Owner, 2026-11-13: "move freely while attacking ... zero slowdown". The keys carry a moving body through a swing at its own
            // pace (SoulsSwordRealInputPlayTests); only a body standing still is stepped, by the stride its attack's clip plants.
            Assert.AreEqual(0.0f, _combat.RunAttack.LungeMetres, "the running attack steps a body the keys are already carrying");
            Assert.AreEqual(0.0f, _combat.OneHandRunAttack.LungeMetres);
            Assert.Greater(_combat.RunAttack.DamageScale, _combat.Light1.DamageScale, "the running attack is no more than the first light");
            Assert.Greater(_combat.OneHandRunAttack.DamageScale, _combat.OneHandLight1.DamageScale);
            Assert.Less(_combat.RunAttack.DamageScale, _combat.Heavy.DamageScale, "a running attack out of nowhere is worth the heavy");
            Assert.Less(_combat.OneHandRunAttack.DamageScale, _combat.OneHandHeavy.DamageScale);
            foreach (SoulsAttack attack in new[] { _combat.OneHandLight1, _combat.OneHandLight2, _combat.OneHandLight3, _combat.OneHandHeavy, _combat.Light1, _combat.Light2,
                         _combat.Light3, _combat.HammerLight3, _combat.Heavy })
            {
                Assert.IsTrue(SwordStrikes.TryGet(attack.Animation, out SwordStrikeKind kind));
                Assert.AreEqual(SwordStrikes.StepMetres(kind), attack.LungeMetres, 1e-4f, $"{attack.Animation} steps the body a different way from the stride its clip plants");
                Assert.GreaterOrEqual(attack.LungeMetres, 0.4f, $"{attack.Animation} barely steps: a body that cannot walk under a swing must be carried in by it");
            }
        }
    }
}
