using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The soulslike sword's rules, stepped by hand (no scene, no input):
    /// commitment, active-frames-only damage, stamina, buffering, chaining and
    /// the roll cancel. Each rule is one a soulslike feels wrong without, and
    /// none of them throws when broken -- a swing that hurts during its
    /// wind-up, or a buffer that never fires, just quietly feels bad.
    ///
    /// The blade here is the fallback one (a 1.8m line straight ahead of the
    /// chest, at 1.4m up), because no SwordWeapon is attached -- the sweep is
    /// the same code either way.
    /// </summary>
    public sealed class SoulsSwordCombatPlayTests
    {
        private GameObject _player;
        private PlayerVitals _vitals;
        private SoulsSwordCombat _combat;
        private GameObject _dummyObject;
        private SparringDummy _dummy;

        /// <summary>A target that answers every hit with a timed-block response.</summary>
        private sealed class TimedBlocker : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                return HitResponse.Blocked;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("SoulsPlayer");
            _player.transform.position = Vector3.zero;
            _player.transform.rotation = Quaternion.identity;
            _vitals = _player.AddComponent<PlayerVitals>();
            _vitals.Roll = () => 1.0f;   // no outright blocks (Block Chance): these guards must behave the same every run
            _vitals.Revive();
            _combat = _player.AddComponent<SoulsSwordCombat>();
            _combat.Equip(null);
            // These tests pin the swing's own timing; the hit-stop (which holds it still on contact)
            // has its own tests at the end of this file.
            _combat.HitStopSeconds = 0.0f;

            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.name = "Dummy";
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            _dummy = _dummyObject.AddComponent<SparringDummy>();
            _dummy.Reset();

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
            if (_dummyObject != null) Object.DestroyImmediate(_dummyObject);
        }

        private void Step(float seconds, float slice = 1.0f / 60.0f)
        {
            for (float t = 0.0f; t < seconds - 0.0001f; t += slice)
            {
                _combat.Tick(Mathf.Min(slice, seconds - t));
                _vitals.TickStamina(false, Mathf.Min(slice, seconds - t));
            }
        }

        // --- commitment / phases -------------------------------------------

        [Test]
        public void ASwingRunsStartupThenActiveThenRecoveryThenEnds()
        {
            _combat.RequestLight(0.0f);
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase);
            Assert.IsTrue(_combat.IsCommitted);

            Step(_combat.Light1.Startup + 0.02f);
            Assert.AreEqual(SoulsPhase.Active, _combat.Phase);

            Step(_combat.Light1.Active + 0.02f);
            Assert.AreEqual(SoulsPhase.Recovery, _combat.Phase);
            Assert.IsTrue(_combat.IsCommitted, "recovery must still root the body");

            Step(_combat.Light1.Recovery + 0.05f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase);
            Assert.IsFalse(_combat.IsCommitted);
        }

        [Test]
        public void TheSwingFacesTheCameraYawTheInstantItStarts()
        {
            _player.transform.rotation = Quaternion.Euler(0.0f, 200.0f, 0.0f);
            _combat.RequestLight(90.0f);

            Assert.AreEqual(90.0f, _player.transform.eulerAngles.y, 0.01f);
        }

        // --- damage only on active frames ------------------------------------

        [Test]
        public void TheBladeDoesNotHurtDuringStartup()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup - 0.03f);

            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase);
            Assert.AreEqual(0, _dummy.TimesHit, "hurt during the wind-up");
        }

        [Test]
        public void TheBladeHurtsDuringActiveFrames()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup + _combat.Light1.Active + 0.02f);

            Assert.AreEqual(1, _dummy.TimesHit, "the swing never connected");
        }

        [Test]
        public void ASwingHitsEachTargetOnlyOnce()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);

            Assert.AreEqual(1, _dummy.TimesHit, "one swing hit the same target more than once");
        }

        [Test]
        public void ANewSwingCanHitTheSameTargetAgain()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);
            Assert.AreEqual(1, _dummy.TimesHit);

            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);
            Assert.AreEqual(2, _dummy.TimesHit);
        }

        [Test]
        public void ATargetOutOfReachIsMissed()
        {
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 6.0f);
            Physics.SyncTransforms();

            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);

            Assert.AreEqual(0, _dummy.TimesHit);
        }

        [Test]
        public void ATargetBehindIsMissedButTurningBringsItIn()
        {
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, -1.5f);
            Physics.SyncTransforms();

            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);
            Assert.AreEqual(0, _dummy.TimesHit, "hit something behind the swing");

            _combat.RequestLight(180.0f);
            Step(_combat.Light1.Total + 0.1f);
            Assert.AreEqual(1, _dummy.TimesHit, "facing the target did not bring it into the swing");
        }

        [Test]
        public void ThePlayersOwnBodyIsNeverHit()
        {
            // A collider on the attacker itself, right in the blade's path.
            var own = new GameObject("OwnCollider");
            own.transform.SetParent(_player.transform, false);
            own.transform.localPosition = new Vector3(0.0f, 1.4f, 1.0f);
            own.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            float before = _vitals.Health;
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);

            Assert.AreEqual(before, _vitals.Health, 0.001f, "the swing hurt its own wielder");
        }

        [Test]
        public void AFastFrameCannotSkipTheActiveWindow()
        {
            // One enormous frame carries the swing across Startup and Active
            // in a single Tick: the hit must still register.
            _combat.RequestLight(0.0f);
            _combat.Tick(_combat.Light1.Startup + _combat.Light1.Active * 0.5f);

            Assert.AreEqual(1, _dummy.TimesHit, "a long frame swallowed the active window");
        }

        [Test]
        public void TheHeavyHitsHarderThanALight()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);
            float lightDamage = _dummy.TotalDamageTaken;

            _dummy.Reset();
            _vitals.Revive();
            Step(1.0f);   // let stamina recover
            _combat.RequestHeavy(0.0f);
            Step(_combat.Heavy.Total + 0.1f);

            Assert.Greater(_dummy.TotalDamageTaken, lightDamage * 1.5f,
                "the heavy is not meaningfully stronger than a light");
        }

        [Test]
        public void TheHeavyIsSlowerAndCostsMoreThanALight()
        {
            Assert.Greater(_combat.Heavy.Startup, _combat.Light1.Startup * 1.5f, "heavy wind-up is not slower");
            Assert.Greater(_combat.Heavy.Recovery, _combat.Light1.Recovery, "heavy recovery is not longer");
            Assert.Greater(_combat.Heavy.StaminaCost, _combat.Light1.StaminaCost, "heavy is not more expensive");
        }

        private SoulsAttack AttackNamed(string attackName)
        {
            switch (attackName)
            {
                case "Light1": return _combat.Light1;
                case "Light2": return _combat.Light2;
                case "Light3": return _combat.Light3;
                case "Heavy": return _combat.Heavy;
                case "OneHandLight1": return _combat.OneHandLight1;
                case "OneHandLight2": return _combat.OneHandLight2;
                case "OneHandLight3": return _combat.OneHandLight3;
                default: return _combat.OneHandHeavy;
            }
        }

        /// <summary>
        /// Every attack is posed by SwordStrikes, and its live window sits on the strike's contact (the fastest moment of the blade's path):
        /// the blade goes live just before it and stops in the follow-through. Pinned so a retune cannot quietly slide the damage window
        /// off the visible cut.
        /// </summary>
        [TestCase("Light1")]
        [TestCase("Light2")]
        [TestCase("Light3")]
        [TestCase("Heavy")]
        [TestCase("OneHandLight1")]
        [TestCase("OneHandLight2")]
        [TestCase("OneHandLight3")]
        [TestCase("OneHandHeavy")]
        public void TheActiveWindowSitsOnTheStrikesContact(string attackName)
        {
            SoulsAttack attack = AttackNamed(attackName);
            Assert.IsTrue(SwordStrikes.TryGet(attack.Animation, out SwordStrikeKind kind), $"{attackName} ('{attack.Animation}') is not a posed strike");
            Assert.IsTrue(attack.IsProcedural);
            Assert.AreEqual(SwordStrikes.Seconds(kind), attack.Total, 0.001f, $"{attackName}: the phases do not add up to the strike's length");

            float contact = SwordStrikes.ContactFraction(kind) * SwordStrikes.Seconds(kind);
            Assert.GreaterOrEqual(contact, attack.Startup - 0.001f,
                $"{attackName}: the blade lands at {contact:0.00}s, before it goes live at {attack.Startup:0.00}s");
            Assert.LessOrEqual(contact, attack.Startup + attack.Active + 0.001f,
                $"{attackName}: the blade lands at {contact:0.00}s, after it stops at {attack.Startup + attack.Active:0.00}s");
            Assert.AreEqual(1.0f, attack.AnimatorSpeed, 0.0001f, "a posed strike runs at its own pace");
            Assert.AreEqual(0.0f, attack.StartSeconds, 0.0001f);
        }

        [Test]
        public void TheOneHandedFirstAttackIsTheHorizontalSlash()
        {
            // Owner, 2026-10-13: "make the first attack with a one handed sword a horizontal slash".
            Assert.AreEqual(SwordStrikes.Slash1HName, _combat.OneHandLight1.Animation);
        }

        [Test]
        public void TheClipSliceIsFittedIntoTheAttacksTotalTime()
        {
            // A clip-backed attack (none is in the moveset now, but the fitting is still how one would play).
            var a = new SoulsAttack("sword_charged_attack", 0.98f, 0.24f, 0.48f, 0.60f, 40.0f, 2.00f, 0.90f, 0.50f, 4.8f, 0.10f, 0.66f);
            float sliceSeconds = a.ClipSeconds * (a.ClipEnd - a.ClipStart);

            Assert.IsFalse(a.IsProcedural);
            Assert.AreEqual(sliceSeconds / a.Total, a.AnimatorSpeed, 0.0001f);
            Assert.AreEqual(a.ClipSeconds * a.ClipStart, a.StartSeconds, 0.0001f);
        }

        [Test]
        public void AnUndescribedClipPlaysAtNormalSpeedFromTheStart()
        {
            var bare = new SoulsAttack("x", 0.1f, 0.1f, 0.1f, 0.5f, 1.0f, 1.0f, 0.1f, 0.0f, 0.0f, 0.0f, 0.0f);

            Assert.AreEqual(1.0f, bare.AnimatorSpeed, 0.0001f);
            Assert.AreEqual(0.0f, bare.StartSeconds, 0.0001f);
        }

        [Test]
        public void TheReachVolumeCatchesALowTargetTheBladeAlone_Misses()
        {
            // Knee height, off to one side: nowhere near the fallback blade
            // (a line at 1.4m up straight ahead), well inside the reach box.
            Object.DestroyImmediate(_dummyObject);
            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.transform.localScale = new Vector3(0.4f, 0.3f, 0.4f);
            _dummyObject.transform.position = new Vector3(0.5f, 0.35f, 1.2f);
            _dummy = _dummyObject.AddComponent<SparringDummy>();
            _dummy.Reset();
            Physics.SyncTransforms();

            _combat.ReachMetres = 0.0f;
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);
            Assert.AreEqual(0, _dummy.TimesHit, "the blade alone was not supposed to reach this");

            _combat.ReachMetres = 2.2f;
            _vitals.Revive();
            Step(1.0f);
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);
            Assert.AreEqual(1, _dummy.TimesHit, "the reach volume did not catch a target within arm's length");
        }

        // --- stamina ---------------------------------------------------------

        [Test]
        public void ASwingSpendsStaminaAndPausesRecovery()
        {
            float before = _vitals.Stamina;
            _combat.RequestLight(0.0f);

            Assert.AreEqual(before - _combat.Light1.StaminaCost, _vitals.Stamina, 0.01f);
            Assert.Greater(_vitals.StaminaRegenDelayRemaining, 0.0f);
        }

        [Test]
        public void StaminaStaysPausedThenRecoversAtTheSoulsRate()
        {
            _vitals.TrySpendCombatStamina(60.0f, 0.0f);   // leave room below the cap to recover into
            _combat.RequestLight(0.0f);
            float spent = _vitals.Stamina;

            _vitals.TickStamina(false, _combat.RegenDelaySeconds * 0.5f);
            Assert.AreEqual(spent, _vitals.Stamina, 0.001f, "recovered during the pause");

            _vitals.TickStamina(false, _combat.RegenDelaySeconds * 0.6f);   // pause ends
            _vitals.TickStamina(false, 1.0f);
            Assert.AreEqual(spent + _combat.StaminaRegenPerSecond, _vitals.Stamina, 0.5f,
                "recovery is not the configured per-second rate");
        }

        [Test]
        public void ASwingIsAllowedWithLessStaminaThanItCostsAndEmptiesTheBar()
        {
            _vitals.TrySpendCombatStamina(_vitals.MaxStamina - 5.0f, 0.0f);
            Assert.Greater(_vitals.Stamina, 0.0f);

            _combat.RequestLight(0.0f);

            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase, "refused a swing with stamina left");
            Assert.AreEqual(0.0f, _vitals.Stamina, 0.001f);
        }

        [Test]
        public void ASwingIsRefusedWithNoStaminaAndTellsYou()
        {
            _vitals.TrySpendCombatStamina(_vitals.MaxStamina * 2.0f, 0.0f);
            int refusals = 0;
            _combat.OutOfStamina += () => refusals++;

            _combat.RequestLight(0.0f);

            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase);
            Assert.AreEqual(1, refusals);
        }

        // --- buffering and chaining -------------------------------------------

        [Test]
        public void AnAttackPressedEarlyIsRememberedAndFiresWhenTheChainWindowOpens()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup * 0.5f);
            _combat.RequestLight(0.0f);   // pressed way too early

            Assert.AreEqual(0, _combat.ChainIndex, "the early press restarted the swing immediately");

            Step(_combat.Light1.Startup * 0.5f + _combat.Light1.Active + 0.02f);
            Assert.AreEqual(SoulsPhase.Recovery, _combat.Phase);
            Assert.AreEqual(0, _combat.ChainIndex, "chained before the window opened");

            Step(_combat.Light1.Recovery * _combat.Light1.ChainAt + 0.03f);
            Assert.AreEqual(1, _combat.ChainIndex, "the buffered press never fired");
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase);
        }

        [Test]
        public void ABufferedPressExpiresWhenThereIsNothingToChainTo()
        {
            // Pressed while the body cannot act (mid-roll) with no swing in progress: the press is only good for BufferSeconds.
            _combat.BufferSeconds = 0.1f;
            _combat.Locked = true;
            _combat.RequestLight(0.0f);
            Step(0.3f);
            _combat.Locked = false;
            Step(0.05f);

            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase, "an expired press still swung");
        }

        [Test]
        public void APressDuringASwingIsRememberedUntilItsChainWindowHoweverLongTheSwing()
        {
            // A press during the wind-up is how a chain is asked for (the research's "buffered inputs fire at the earliest legal moment"), so it
            // is kept until this swing's window opens even when the swing outlasts the plain buffer by far.
            _combat.BufferSeconds = 0.1f;
            _combat.RequestLight(0.0f);
            Step(0.05f);
            _combat.RequestLight(0.0f);
            Assert.Greater(_combat.SecondsUntilChainWindow(), 0.3f, "setup: the first swing's window should be well past the buffer");

            Step(_combat.Light1.Startup + _combat.Light1.Active + _combat.Light1.Recovery * _combat.Light1.ChainAt + 0.03f);

            Assert.AreEqual(1, _combat.ChainIndex, "the press made during the swing was dropped before its window");
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase);
        }

        [Test]
        public void TheLightChainCyclesThroughThreeSwingsThenStartsOver()
        {
            var seen = new System.Collections.Generic.List<int>();
            _combat.AttackStarted += _ => seen.Add(_combat.ChainIndex);

            // Mashing: a click every 50ms for long enough to run the whole chain.
            _vitals.InfiniteStamina = true;
            for (float t = 0.0f; t < 6.0f; t += 0.05f)
            {
                _combat.RequestLight(0.0f);
                Step(0.05f);
            }

            Assert.GreaterOrEqual(seen.Count, 4, "the chain did not continue");
            Assert.AreEqual(0, seen[0]);
            Assert.AreEqual(1, seen[1]);
            Assert.AreEqual(2, seen[2]);
            Assert.AreEqual(0, seen[3], "the chain did not wrap around to the first swing");
        }

        [Test]
        public void ALateFreshPressAfterTheChainEndsStartsTheFirstSwingAgain()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Total + 0.1f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase);

            _combat.RequestLight(0.0f);
            Assert.AreEqual(0, _combat.ChainIndex);
        }

        [Test]
        public void ARequestWhileLockedWaitsForTheLockToClear()
        {
            _combat.Locked = true;
            _combat.RequestLight(0.0f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase, "swung while locked out (mid-roll)");

            _combat.Locked = false;
            _combat.Tick(0.016f);
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase, "the remembered press never fired after the lock cleared");
        }

        // --- roll cancel --------------------------------------------------------

        [Test]
        public void ARollIsRefusedDuringStartupAndActiveButAllowedInRecovery()
        {
            _combat.RequestLight(0.0f);
            Assert.IsFalse(_combat.CanRoll, "rolled out of startup");

            Step(_combat.Light1.Startup + 0.02f);
            Assert.AreEqual(SoulsPhase.Active, _combat.Phase);
            Assert.IsFalse(_combat.CanRoll, "rolled out of the active frames");

            Step(_combat.Light1.Active + 0.02f);
            Assert.AreEqual(SoulsPhase.Recovery, _combat.Phase);
            Assert.IsTrue(_combat.CanRoll, "could not roll out of recovery");
        }

        [Test]
        public void RollingOutOfRecoveryEndsTheSwingAndFreesTheBody()
        {
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup + _combat.Light1.Active + 0.02f);

            _combat.BeginRoll();

            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase);
            Assert.IsFalse(_combat.IsCommitted);
        }

        // --- hit reactions --------------------------------------------------------

        [Test]
        public void BeingHitMidSwingCancelsTheSwing()
        {
            _combat.RequestLight(0.0f);
            Step(0.05f);
            Assert.IsTrue(_combat.IsCommitted);

            ((IDamageable)_vitals).ApplyHit(10.0f, new Vector3(0.0f, 0.0f, 5.0f), 0.5f);
            _combat.Tick(0.016f);

            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase, "kept swinging while staggered");
        }

        [Test]
        public void HavingYourSwingTimedBlockedLeavesYouStaggered()
        {
            Object.DestroyImmediate(_dummyObject);
            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            _dummyObject.AddComponent<TimedBlocker>();
            Physics.SyncTransforms();

            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup + _combat.Light1.Active + 0.02f);

            Assert.IsTrue(_vitals.IsStunned, "a timed block did not punish the attacker");
        }

        [Test]
        public void HavingYourSwingMerelyGuardedDoesNotStaggerYou()
        {
            Object.DestroyImmediate(_dummyObject);
            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            var victim = _dummyObject.AddComponent<PlayerVitals>();
            victim.Roll = () => 1.0f;   // no outright blocks (Block Chance)
            victim.Revive();
            victim.BlockDrainsStamina = true;
            victim.StartBlock();
            Physics.SyncTransforms();

            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup + _combat.Light1.Active + 0.02f);

            Assert.IsFalse(_vitals.IsStunned, "an ordinary held guard stunned its attacker");
            Assert.Less(victim.Stamina, victim.MaxStamina, "the guard did not pay stamina for the hit");
        }

        // --- guard ----------------------------------------------------------------

        [Test]
        public void AHeldBlockStaysUpForAsLongAsItIsHeld()
        {
            for (int i = 0; i < 120; i++)
            {
                _combat.SetBlockHeld(true);
                _vitals.TickHitstun(1.0f / 60.0f);
            }

            Assert.IsTrue(_vitals.IsBlocking, "a held block timed out on its own");
            Assert.IsTrue(_combat.IsBlocking);

            _combat.SetBlockHeld(false);
            Assert.IsFalse(_vitals.IsBlocking, "releasing the button did not drop the guard");
        }

        [Test]
        public void SwingingDropsAHeldBlock()
        {
            _combat.SetBlockHeld(true);
            Assert.IsTrue(_combat.IsBlocking);

            _combat.RequestLight(0.0f);

            Assert.IsFalse(_combat.IsBlocking);
            Assert.IsFalse(_vitals.IsBlocking);
        }

        [Test]
        public void AHeldGuardCannotBeRaisedMidSwing()
        {
            _combat.RequestLight(0.0f);
            _combat.SetBlockHeld(true);

            Assert.IsFalse(_combat.IsBlocking, "guarded in the middle of a swing");
        }

        // --- console tuning ---------------------------------------------------------

        // --- poise, hyper-armour and the parry (2026-10-13) --------------------

        private void BlowOnSelf(float poiseDamage)
        {
            _vitals.TakePoise(poiseDamage);
            ((IDamageable)_vitals).ApplyHit(5.0f, _player.transform.position + Vector3.forward, 0.4f);
        }

        [Test]
        public void AHeavyKeepsSwingingThroughLightBlowsUntilItsPoiseBreaks()
        {
            _vitals.InfiniteStamina = true;
            _combat.RequestHeavy(0.0f);
            Step(0.1f);
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase);
            Assert.IsTrue(_vitals.HyperArmour, "a heavy's wind-up should carry hyper-armour");

            BlowOnSelf(25.0f);
            Step(1.0f / 60.0f);
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase, "one light blow should not stop a heavy");
            BlowOnSelf(25.0f);
            Step(1.0f / 60.0f);
            Assert.AreEqual(SoulsPhase.Startup, _combat.Phase, "two light blows should not stop a heavy");

            BlowOnSelf(25.0f);
            Step(1.0f / 60.0f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase, "the third blow empties the poise: the heavy is broken");
            Assert.IsTrue(_vitals.IsStunned);
            Assert.IsFalse(_vitals.HyperArmour, "hyper-armour is gone with the swing");
        }

        [Test]
        public void ALightIsBrokenByAnyBlow()
        {
            _combat.RequestLight(0.0f);
            Step(0.1f);
            Assert.IsFalse(_vitals.HyperArmour, "a light has no hyper-armour");

            BlowOnSelf(20.0f);
            Step(1.0f / 60.0f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase, "a blow during a light should break it");
            Assert.IsTrue(_vitals.IsStunned);
        }

        [Test]
        public void TheGuardsFirstInstantParriesAndLaterOnlyBlocks()
        {
            _combat.SetBlockHeld(true);
            Assert.Greater(_vitals.PerfectGuardRemaining, 0.0f, "raising the guard should open the parry window");
            float stamina = _vitals.Stamina;

            HitResponse early = ((IDamageable)_vitals).ApplyHit(30.0f, Vector3.forward * 2.0f, 0.4f);
            Assert.AreEqual(HitResponse.Blocked, early, "a blow in the guard's first instant is parried");
            Assert.AreEqual(stamina, _vitals.Stamina, 0.001f);

            for (int i = 0; i < 30; i++)
            {
                _combat.SetBlockHeld(true);
                _combat.Tick(1.0f / 60.0f);
                _vitals.TickHitstun(1.0f / 60.0f);
            }
            HitResponse late = ((IDamageable)_vitals).ApplyHit(30.0f, Vector3.forward * 2.0f, 0.4f);
            Assert.AreEqual(HitResponse.Guarded, late, "half a second in, the guard only soaks");
        }

        [Test]
        public void AnotherPlayersVerdictOfParriedStaggersThisAttacker()
        {
            Assert.IsFalse(_vitals.IsStunned);
            _combat.ApplyRemoteVerdict(HitResponse.Guarded);
            Assert.IsFalse(_vitals.IsStunned, "a soaked blow does not punish the attacker");
            _combat.ApplyRemoteVerdict(HitResponse.Ignored);
            Assert.IsFalse(_vitals.IsStunned, "a rolled-through blow does not punish the attacker");
            _combat.ApplyRemoteVerdict(HitResponse.Blocked);
            Assert.IsTrue(_vitals.IsStunned, "a parry on the other player's machine must stagger this attacker");
            Assert.AreEqual(_combat.BlockedRecoilSeconds, _vitals.HitstunRemaining, 0.001f);
        }

        [Test]
        public void SwordConsoleTuningChangesTheLiveNumbers()
        {
            Assert.IsTrue(_combat.TrySet("Heavy.Startup", 0.9f, out string message), message);
            Assert.AreEqual(0.9f, _combat.Heavy.Startup, 0.0001f);

            Assert.IsTrue(_combat.TrySet("bufferseconds", 0.25f, out message), message);
            Assert.AreEqual(0.25f, _combat.BufferSeconds, 0.0001f);
        }

        [Test]
        public void SwordConsoleTuningRejectsGarbage()
        {
            Assert.IsFalse(_combat.TrySet("Nonsense", 1.0f, out string message));
            StringAssert.Contains("Nonsense", message);
            Assert.IsFalse(_combat.TrySet("Heavy.Nonsense", 1.0f, out _));
            Assert.IsFalse(_combat.TrySet("Heavy.Startup.Extra", 1.0f, out _));
        }

        [Test]
        public void SwordConsoleListsEveryAttack()
        {
            string text = _combat.Describe();

            StringAssert.Contains("Light1", text);
            StringAssert.Contains("Light2", text);
            StringAssert.Contains("Light3", text);
            StringAssert.Contains("Heavy", text);
            StringAssert.Contains("BufferSeconds", text);
        }

        [Test]
        public void UnequippingDropsTheSwingTheGuardAndTheFasterRegen()
        {
            _combat.RequestLight(0.0f);
            _vitals.StaminaRegenPerSecondOverride = 45.0f;

            _combat.Unequip(null);

            Assert.IsFalse(_combat.IsCommitted);
            Assert.IsFalse(_combat.IsEquipped);
            Assert.AreEqual(0.0f, _vitals.StaminaRegenPerSecondOverride, 0.0001f);
        }

        // --- hit-stop ------------------------------------------------------------

        /// <summary>How long a swing takes to reach its recovery, stepping 1/60s at a time.</summary>
        private float SecondsUntilRecovery()
        {
            _combat.RequestLight(0.0f);
            float elapsed = 0.0f;
            while (_combat.Phase != SoulsPhase.Recovery && elapsed < 3.0f)
            {
                Step(1.0f / 60.0f);
                elapsed += 1.0f / 60.0f;
            }
            return elapsed;
        }

        [Test]
        public void ALandedHitHoldsTheWholeSwingStillForTheHitStop()
        {
            _combat.HitStopSeconds = 0.10f;

            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 30.0f);   // nothing to hit
            Physics.SyncTransforms();
            float missed = SecondsUntilRecovery();
            Step(2.0f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase);

            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            Physics.SyncTransforms();
            float hit = SecondsUntilRecovery();

            Assert.AreEqual(missed + 0.10f, hit, 0.04f,
                $"a swing that connects should take one hit-stop longer to reach its recovery ({missed:0.000}s missing, {hit:0.000}s hitting)");
            Assert.AreEqual(1, _dummy.TimesHit);
        }

        [Test]
        public void HitStopEndsAndDoesNotStickTheSwing()
        {
            _combat.HitStopSeconds = 0.10f;
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup + 0.03f);
            Assert.IsTrue(_combat.IsInHitStop, "the blade connected but nothing held the swing");

            Step(0.25f);
            Assert.IsFalse(_combat.IsInHitStop, "the hit-stop never let go");

            Step(_combat.Light1.Total + 0.5f);
            Assert.AreEqual(SoulsPhase.Idle, _combat.Phase, "the swing never finished after its hit-stop");
        }

        [Test]
        public void InterruptingDuringAHitStopClearsIt()
        {
            _combat.HitStopSeconds = 0.5f;
            _combat.RequestLight(0.0f);
            Step(_combat.Light1.Startup + 0.03f);
            Assert.IsTrue(_combat.IsInHitStop);

            _combat.Interrupt();

            Assert.IsFalse(_combat.IsInHitStop, "a dropped swing left the hit-stop running");
        }

        [Test]
        public void AHeavyHitStopsLongerThanALightOne()
        {
            _combat.HitStopSeconds = 0.10f;
            _combat.HeavyHitStopMultiplier = 2.0f;

            _combat.RequestHeavy(0.0f);
            Step(_combat.Heavy.Startup + 0.03f);
            Assert.IsTrue(_combat.IsInHitStop);
            Step(0.15f);
            Assert.IsTrue(_combat.IsInHitStop, "a heavy hit should still be held at 0.15s of a 0.2s stop");
            Step(0.1f);
            Assert.IsFalse(_combat.IsInHitStop);
        }
    }
}
