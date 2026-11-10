using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The attack chain in motion.
    ///
    /// The chain is what made the Godot build feel good, and its failure modes
    /// are all timing: a window that never closes so the combo can never
    /// restart, a finisher that loops, a swing that never releases the body.
    /// That last one is still an open bug in the Godot build (task #292). None
    /// of them throw, and all of them need a specific sequence to show up --
    /// exactly what a test is for and a playthrough is bad at.
    ///
    /// Stepped by hand rather than run as coroutines, so a case can advance
    /// 0.9 seconds in one call instead of waiting for it.
    /// </summary>
    public class MeleeCombatPlayTests
    {
        private GameObject _attackerObject;
        private MeleeAttacker _attacker;
        private GameObject _dummyObject;
        private SparringDummy _dummy;

        [SetUp]
        public void SetUp()
        {
            _attackerObject = new GameObject("Attacker");
            _attackerObject.transform.position = Vector3.zero;
            _attackerObject.transform.rotation = Quaternion.identity;
            _attacker = _attackerObject.AddComponent<MeleeAttacker>();

            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.name = "Dummy";
            // Inside a fist's 1.4 m reach (its near surface a metre away), directly in front (+Z is forward at
            // identity rotation).
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            _dummy = _dummyObject.AddComponent<SparringDummy>();
            _dummy.Reset();

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackerObject != null) Object.DestroyImmediate(_attackerObject);
            if (_dummyObject != null) Object.DestroyImmediate(_dummyObject);
        }

        /// <summary>Swings once and runs the swing out to completion.</summary>
        private void SwingOnce()
        {
            Assert.IsTrue(_attacker.TryAttack(), "the attacker refused to swing");
            Step(FullSwing());
        }

        /// <summary>How long the swing in progress takes to play out (each unarmed strike has its own length: see FistStrikes), plus a hair.</summary>
        private float FullSwing()
        {
            float seconds = FistStrikes.TryGet(_attacker.CurrentStage.Animation, out FistKind kind) ? FistStrikes.Seconds(kind) : _attacker.SwingSeconds;
            return seconds + 0.01f;
        }

        /// <summary>Advances time in small slices so contact moments are not skipped.</summary>
        private void Step(float seconds, float slice = 1.0f / 60.0f)
        {
            for (float t = 0.0f; t < seconds; t += slice)
            {
                _attacker.Tick(Mathf.Min(slice, seconds - t));
            }
        }

        [Test]
        public void APunchInReachLandsAndHurts()
        {
            SwingOnce();

            Assert.AreEqual(1, _dummy.TimesHit, "the punch did not connect");
            Assert.AreEqual(CombatTuning.DamageFor(ComboChain.For(ComboChain.Unarmed)[0], 0), _dummy.TotalDamageTaken, 0.01f);
            Assert.Less(_dummy.Health, _dummy.MaxHealth);
        }

        [Test]
        public void ATargetOutOfReachIsNotHit()
        {
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, _attacker.Range + 3.0f);
            Physics.SyncTransforms();

            SwingOnce();
            Assert.AreEqual(0, _dummy.TimesHit, "punched something well out of range");
        }

        /// <summary>A fist reaches less far than a weapon: a target a sword would reach (its surface 1.7 m off) is out of a punch's reach.</summary>
        [Test]
        public void AFistReachesLessFarThanAWeapon()
        {
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, MeleeAttacker.FistReachMetres + 0.8f);
            Physics.SyncTransforms();

            SwingOnce();
            Assert.AreEqual(0, _dummy.TimesHit, "a bare fist reached as far as a weapon");
        }

        /// <summary>
        /// Reach alone is not enough -- a swing has a front. Without the arc
        /// check you hit things standing behind you, which reads as the game
        /// cheating on your behalf.
        /// </summary>
        [Test]
        public void ATargetBehindTheAttackerIsNotHit()
        {
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, -1.5f);
            Physics.SyncTransforms();

            SwingOnce();
            Assert.AreEqual(0, _dummy.TimesHit, "punched something standing behind");
        }

        [Test]
        public void TurningToFaceATargetBringsItIntoTheArc()
        {
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, -1.5f);
            Physics.SyncTransforms();

            SwingOnce();
            Assert.AreEqual(0, _dummy.TimesHit);

            _attackerObject.transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            Step(1.0f);   // let the combo window lapse
            SwingOnce();
            Assert.AreEqual(1, _dummy.TimesHit, "turning to face the target did not help");
        }

        /// <summary>
        /// The body is busy during a swing. Without this, holding the button
        /// fires every frame and the whole chain resolves instantly.
        /// </summary>
        [Test]
        public void TheBodyIsBusyForTheDurationOfASwing()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Assert.IsTrue(_attacker.IsSwinging);
            Assert.IsFalse(_attacker.TryAttack(), "started a second swing during the first");

            Step(FistStrikes.Seconds(FistKind.RightStraight) * 0.2f);
            Assert.IsFalse(_attacker.TryAttack(), "swing released early in the windup");

            // The presses above were remembered (input buffering), so the next punch comes by itself; let the whole chain play out and finish.
            Step(4.0f);
            Assert.IsFalse(_attacker.IsSwinging, "the swing never released the body");
            Assert.IsTrue(_attacker.TryAttack(), "could not swing again after the swing finished");
        }

        /// <summary>A punch pressed while the last is still going is remembered and thrown as soon as it allows, instead of being lost.</summary>
        [Test]
        public void APunchPressedWhileBusyIsBufferedAndThrownAsSoonAsItAllows()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(FistStrikes.RightStraightName, _attacker.CurrentStage.Animation);
            Step(0.05f);
            Assert.IsFalse(_attacker.TryAttack(), "a second swing started in the middle of the first");   // remembered, not thrown

            Step(FistStrikes.Seconds(FistKind.RightStraight));
            Assert.AreEqual(FistStrikes.LeftStraightName, _attacker.CurrentStage.Animation, "the remembered press was lost");
            Assert.IsTrue(_attacker.IsSwinging);
        }

        /// <summary>The recovery of a punch can be cancelled into the next one: the chain flows instead of waiting out every tail.</summary>
        [Test]
        public void TheRecoveryOfAPunchCanBeCancelledIntoTheNext()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Step(FistStrikes.Seconds(FistKind.RightStraight) * 0.8f);   // past the contact, in the recovery
            Assert.IsTrue(_attacker.TryAttack(), "the next punch could not cancel the recovery");
            Assert.AreEqual(FistStrikes.LeftStraightName, _attacker.CurrentStage.Animation);
        }

        /// <summary>
        /// The chain's closing punch (the left straight) must be recovered from in full, as in Souls: a press in its recovery is kept and thrown
        /// only once it is over, from the start of the chain again.
        /// </summary>
        [Test]
        public void TheClosingPunchCannotBeCancelledAndRestartsTheChain()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Step(FullSwing());
            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(FistStrikes.LeftStraightName, _attacker.CurrentStage.Animation);
            Step(FistStrikes.Seconds(FistKind.LeftStraight) * 0.8f);   // in the recovery
            Assert.IsFalse(_attacker.TryAttack(), "the closing punch's recovery was cancelled");
            Assert.AreEqual(FistStrikes.LeftStraightName, _attacker.CurrentStage.Animation);

            Step(FistStrikes.Seconds(FistKind.LeftStraight) * 0.2f + 0.05f);
            Assert.IsTrue(_attacker.IsSwinging, "the press in the recovery was lost");
            Assert.AreEqual(FistStrikes.RightStraightName, _attacker.CurrentStage.Animation, "the chain did not start over after its closing punch");
            Assert.Greater(_dummy.LastHitstun, 0.0f, "the closing punch did not stun");
        }

        /// <summary>The held punch is the overhand; held again in its window, the uppercut; a light after the overhand is the chain from the start.</summary>
        [Test]
        public void TheHeldPunchIsTheOverhandAndHeldAgainTheUppercut()
        {
            Assert.IsTrue(_attacker.TryAttack(heavy: true));
            Assert.AreEqual(FistStrikes.OverhandName, _attacker.CurrentStage.Animation);
            Step(FullSwing());
            Assert.AreEqual(0.0f, _dummy.LastHitstun, 0.001f, "the overhand stunned as well as knocking down");
            float overhand = _dummy.TotalDamageTaken;

            Assert.IsTrue(_attacker.TryAttack(heavy: true));
            Assert.AreEqual(FistStrikes.UppercutName, _attacker.CurrentStage.Animation, "the held follow-up to the overhand is not the uppercut");
            Step(FullSwing());
            Assert.Greater(_dummy.LastHitstun, 0.0f, "the uppercut did not stun");
            Assert.Less(_dummy.TotalDamageTaken - overhand, overhand, "the uppercut out-hit the overhand");

            Assert.IsTrue(_attacker.TryAttack(heavy: true));
            Assert.AreEqual(FistStrikes.OverhandName, _attacker.CurrentStage.Animation, "the uppercut opened a window of its own");
            Step(FullSwing());
            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(FistStrikes.RightStraightName, _attacker.CurrentStage.Animation, "a light after the overhand is not the chain's first punch");
        }

        /// <summary>No stamina, no punch (as with the sword); a punch with any left is allowed, and bottoms the bar out.</summary>
        [Test]
        public void PunchesCostStaminaAndStopWhenItIsGone()
        {
            var vitals = _attackerObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            Object.DestroyImmediate(_attacker);
            _attacker = _attackerObject.AddComponent<MeleeAttacker>();

            float before = vitals.Stamina;
            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(before - CombatTuning.PunchStaminaCost, vitals.Stamina, 0.01f, "a straight did not cost its stamina");
            Step(FullSwing());

            vitals.TrySpendCombatStamina(vitals.Stamina - 1.0f, 0.0f);   // down to the last point
            Assert.IsTrue(_attacker.TryAttack(), "a punch with a little stamina left was refused");
            Assert.AreEqual(0.0f, vitals.Stamina, 0.01f);
            Step(FullSwing() + 0.1f);
            Assert.IsFalse(_attacker.TryAttack(), "a punch was thrown on empty");
            Assert.IsFalse(_attacker.TryAttack(heavy: true), "an overhand was thrown on empty");
        }

        /// <summary>
        /// A punch roots the body and carries it into the blow by its own stride (its clip's feet are planted on the ground it is carried
        /// over), and no further once it has landed.
        /// </summary>
        [Test]
        public void APunchRootsTheBodyAndCarriesItIntoTheBlowByItsStride()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(0.0f, _attacker.SwingMoveFraction, 0.001f, "the body walks about under a punch whose feet are planted");
            float stride = FistStrikes.CarryMetres(FistKind.RightStraight);
            float lands = FistStrikes.Seconds(FistKind.RightStraight) * FistStrikes.ContactFraction(FistKind.RightStraight);
            Step(lands * 0.6f);
            Assert.That(_attacker.PunchCarried, Is.InRange(0.05f, stride - 0.05f), "part way to landing the body is not part way through its stride");
            Step(lands * 0.4f + 0.02f);
            Assert.AreEqual(stride, _attacker.PunchCarried, 0.001f, "the stride is not done when the blow lands");
            Step(FistStrikes.Seconds(FistKind.RightStraight) * 0.3f);
            Assert.AreEqual(stride, _attacker.PunchCarried, 0.001f, "the body is carried on after the blow has landed");
        }

        [Test]
        public void PunchingRepeatedlyWalksTheChainAndEndsOnTheFinisher()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);

            for (int i = 0; i < chain.Length; i++)
            {
                Assert.IsTrue(_attacker.TryAttack(), $"could not start stage {i}");
                Assert.AreEqual(chain[i].Animation, _attacker.CurrentStage.Animation,
                    $"stage {i} played the wrong animation");
                Step(FullSwing());
            }

            Assert.AreEqual(chain.Length, _dummy.TimesHit, "not every stage of the chain landed");
            Assert.AreEqual(0, _attacker.NextStageIndex,
                "the chain did not reset after its finisher");
        }

        /// <summary>
        /// Waiting too long has to start the chain over. Without the window
        /// closing, one punch a minute apart would still reach the finisher.
        /// </summary>
        [Test]
        public void LettingTheWindowLapseRestartsTheChain()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);

            Assert.IsTrue(_attacker.TryAttack());
            Step(FullSwing());
            Assert.AreEqual(1, _attacker.NextStageIndex, "the chain did not advance");

            // Well past the first stage's window.
            Step(chain[0].WindowSeconds + 0.5f);
            Assert.AreEqual(0, _attacker.NextStageIndex, "the window never closed");

            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(chain[0].Animation, _attacker.CurrentStage.Animation,
                "did not restart from the first stage");
        }

        [Test]
        public void PunchingInsideTheWindowKeepsTheChainGoing()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);

            Assert.IsTrue(_attacker.TryAttack());
            Step(FullSwing());

            // Comfortably inside the window.
            Step(chain[0].WindowSeconds * 0.4f);
            Assert.IsTrue(_attacker.TryAttack());
            Assert.AreEqual(chain[1].Animation, _attacker.CurrentStage.Animation,
                "the second punch did not continue the chain");
        }

        /// <summary>
        /// A whiffed punch still advances. The original did this so the chain
        /// reads as a continuous move rather than stalling whenever a swing
        /// misses.
        /// </summary>
        [Test]
        public void MissingStillAdvancesTheChain()
        {
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 50.0f);
            Physics.SyncTransforms();

            Assert.IsTrue(_attacker.TryAttack());
            Step(FullSwing());

            Assert.AreEqual(0, _dummy.TimesHit, "somehow hit a target 50m away");
            Assert.AreEqual(1, _attacker.NextStageIndex, "a miss stalled the chain");
        }

        [Test]
        public void EachSwingLandsExactlyOnceNoMatterHowFinelyItIsStepped()
        {
            Assert.IsTrue(_attacker.TryAttack());
            for (int i = 0; i < 400; i++)
            {
                _attacker.Tick(0.001f);
            }
            Assert.AreEqual(1, _dummy.TimesHit, "one swing landed more than once");
        }

        /// <summary>
        /// A single frame longer than the whole swing must still land the blow.
        /// A hitch swallowing a punch is a miserable, unreproducible bug.
        /// </summary>
        [Test]
        public void ASwingResolvedInOneLongFrameStillLands()
        {
            Assert.IsTrue(_attacker.TryAttack());
            _attacker.Tick(2.0f);

            Assert.AreEqual(1, _dummy.TimesHit, "a long frame swallowed the punch");
            Assert.IsFalse(_attacker.IsSwinging);
        }

        [Test]
        public void ADeadTargetStopsTakingHits()
        {
            _dummy.MaxHealth = 5.0f;
            _dummy.Reset();

            SwingOnce();
            Assert.IsFalse(_dummy.IsAlive, "should have been felled by one punch");
            int hitsWhenFelled = _dummy.TimesHit;

            Step(2.0f);
            SwingOnce();
            Assert.AreEqual(hitsWhenFelled, _dummy.TimesHit, "kept hitting a felled target");
        }

        [Test]
        public void StrengthMakesPunchesHurtMore()
        {
            var vitals = _attackerObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            vitals.Strength = 50;

            // Re-fetch: MeleeAttacker looked for vitals in its own Awake, which
            // has already run.
            Object.DestroyImmediate(_attacker);
            _attacker = _attackerObject.AddComponent<MeleeAttacker>();

            SwingOnce();
            Assert.AreEqual(CombatTuning.DamageFor(ComboChain.For(ComboChain.Unarmed)[0], 0) * 2.0f, _dummy.TotalDamageTaken, 0.05f,
                "50 Strength at 2% a point should double the damage");
        }

        [Test]
        public void ADeadAttackerCannotSwing()
        {
            var vitals = _attackerObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            Object.DestroyImmediate(_attacker);
            _attacker = _attackerObject.AddComponent<MeleeAttacker>();

            vitals.TakeDamage(10000.0f);
            Assert.IsTrue(vitals.IsDead);
            Assert.IsFalse(_attacker.TryAttack(), "a corpse threw a punch");
        }

        /// <summary>Bare-minimum sword: MovesetId routes TrySpinAttack's own gate to accept it.</summary>
        private sealed class FakeSwordWeapon : IEquippableWeapon
        {
            public float GetMeleeDamage() => 20.0f;
            public float MeleeCooldownSeconds => 0.4f;
            public void RegisterLandedMeleeHit() { }
            public bool CanThrow() => false;
            public float ThrowSpeed => 0.0f;
            public void Throw(GameObject player, Vector3 origin, Vector3 direction) { }
            public string MovesetId => ComboChain.Sword;
            public ComboStage? HeavyStage => null;
        }

        /// <summary>A non-sword weapon -- proves TrySpinAttack's gate checks MovesetId specifically, not just "something is equipped".</summary>
        private sealed class FakeBottleWeapon : IEquippableWeapon
        {
            public float GetMeleeDamage() => 5.0f;
            public float MeleeCooldownSeconds => 0.3f;
            public void RegisterLandedMeleeHit() { }
            public bool CanThrow() => true;
            public float ThrowSpeed => 10.0f;
            public void Throw(GameObject player, Vector3 origin, Vector3 direction) { }
            public string MovesetId => null;
            public ComboStage? HeavyStage => null;
        }

        /// <summary>
        /// Requested: "add a spin attack with the sword press 2 to use
        /// it. its like links up special in smash bros he just spins
        /// around with the sword for a bit."
        /// </summary>
        [Test]
        public void SpinAttackRequiresASwordSpecifically()
        {
            Assert.IsFalse(_attacker.TrySpinAttack(), "spun with bare fists");

            _attacker.EquippedWeapon = new FakeBottleWeapon();
            Assert.IsFalse(_attacker.TrySpinAttack(), "spun with a non-sword weapon equipped");

            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack(), "refused to spin with a real sword equipped");
        }

        /// <summary>
        /// THE WHOLE POINT of a spin over an ordinary swing -- ATargetBehindTheAttackerIsNotHit
        /// already proves an ordinary swing's own forward arc misses this
        /// exact position.
        /// </summary>
        [Test]
        public void SpinAttackHitsAllTheWayAroundNotJustInFront()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, -1.5f);
            Physics.SyncTransforms();

            Assert.IsTrue(_attacker.TrySpinAttack());
            Step(_attacker.SpinAttackSeconds + 0.05f);

            Assert.Greater(_dummy.TimesHit, 0, "a target directly behind the attacker was never hit by the spin");
        }

        /// <summary>
        /// FOUR BLOWS (owner, 2026-11-05: "make it hit 4 times so 4 times the damage number pops up"), one each turn, together what Link's one
        /// clean hit was (14%, a light swing being 10%). The fake sword is worth 20 here, so the four together are 20 x 1.4.
        /// </summary>
        [Test]
        public void TheGroundSpinHitsATargetFourTimes()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack());
            Step(_attacker.SpinAttackSeconds + 0.05f);

            Assert.AreEqual(4, _dummy.TimesHit, "a ground spin lands four blows, not one and not one a frame");
            Assert.AreEqual(20.0f * 1.4f, _dummy.TotalDamageTaken, 0.01f, "the four together are the old clean hit");
        }

        /// <summary>Something that walks into reach late is hit only by the blows still to come.</summary>
        [Test]
        public void ATargetThatArrivesLateIsHitOnlyByTheBlowsLeft()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 8.0f);   // far out of reach to begin with
            Physics.SyncTransforms();
            Assert.IsTrue(_attacker.TrySpinAttack());
            Step(0.41f);   // frame 24-25: past the first two blows (frames 7 and 15)

            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);
            Physics.SyncTransforms();
            Step(_attacker.SpinAttackSeconds);

            Assert.AreEqual(2, _dummy.TimesHit, "arriving after two blows, it takes the last two");
        }

        /// <summary>Holding the key charges it, up to a second, for 1 + 0.6 x charge: a full second is 1.6x.</summary>
        [Test]
        public void ChargingAGroundSpinForAFullSecondDoes1Point6Times()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack(charge: true));
            Assert.IsTrue(_attacker.IsChargingSpin);
            Assert.IsFalse(_attacker.IsSpinTurning, "it should be waiting for the key to be let go, not spinning");
            Step(0.2f);
            Assert.AreEqual(0, _dummy.TimesHit, "a spin that is still charging has hit nothing");

            Step(1.0f);   // past the full second: it lets go by itself
            Assert.IsTrue(_attacker.IsSpinTurning || _dummy.TimesHit > 0, "a full charge should release by itself");
            Step(_attacker.SpinAttackSeconds);

            Assert.AreEqual(4, _dummy.TimesHit);
            Assert.AreEqual(20.0f * 1.4f * 1.6f, _dummy.TotalDamageTaken, 0.05f);
        }

        [Test]
        public void ReleasingAHalfChargedSpinDoes1Point3Times()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack(charge: true));
            Step(0.5f);
            _attacker.ReleaseSpinAttack();
            Assert.IsTrue(_attacker.IsSpinTurning, "letting go should start the spin");
            Step(_attacker.SpinAttackSeconds);

            Assert.AreEqual(20.0f * 1.4f * 1.3f, _dummy.TotalDamageTaken, 0.1f);
        }

        /// <summary>A tap is not a charge: with no key to wait on, it goes at once.</summary>
        [Test]
        public void ATapSpinsAtOnceWithNoChargeToWaitFor()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack());
            Assert.IsTrue(_attacker.IsSpinTurning);
            Assert.IsFalse(_attacker.IsChargingSpin);
        }

        /// <summary>Hits do not need an animation clip to say when: spun with no animator at all the ground spin lasts 74 frames (interruptible).</summary>
        [Test]
        public void TheGroundSpinHoldsTheBodySeventyFourFrames()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack());
            Assert.AreEqual(74.0f / 60.0f, _attacker.SpinAttackSeconds, 1e-4f);
            Step(73.0f / 60.0f);
            Assert.IsTrue(_attacker.IsSpinAttacking, "let go of the body before frame 74");
            Step(3.0f / 60.0f);
            Assert.IsFalse(_attacker.IsSpinAttacking, "still holding the body after frame 74");
        }

        /// <summary>Matches PlayerInputReader's own "Swinging roots the body" movement gate, which reads this same flag.</summary>
        [Test]
        public void SpinAttackRootsTheBodyAndRefusesToRestartWhileSpinning()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack());
            Assert.IsTrue(_attacker.IsSwinging, "IsSwinging should cover an active spin, the same flag movement already checks");
            Assert.IsTrue(_attacker.IsSpinAttacking);

            Assert.IsFalse(_attacker.TrySpinAttack(), "restarted a spin while one was already in progress");
            Assert.IsFalse(_attacker.TryAttack(), "threw an ordinary swing while spinning");

            Step(_attacker.SpinAttackSeconds + 0.05f);
            Assert.IsFalse(_attacker.IsSpinAttacking, "the spin never released the body");
        }

        [Test]
        public void SpinAttackHasACooldownAfterItEnds()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            Assert.IsTrue(_attacker.TrySpinAttack());
            Step(_attacker.SpinAttackSeconds + 0.05f);

            Assert.IsFalse(_attacker.IsSpinAttacking, "setup failed -- the spin should have finished");
            Assert.IsFalse(_attacker.TrySpinAttack(), "spun again immediately with no cooldown at all");

            Step(_attacker.SpinAttackCooldownSeconds + 0.05f);
            Assert.IsTrue(_attacker.TrySpinAttack(), "still refused to spin after its own cooldown fully elapsed");
        }

        /// <summary>
        /// The turn is inside the spin's animation (SwordSpinMoveTests): the body's own facing never moves, so the camera, what the body faces
        /// and other players' copies are left alone, and it ends facing where it began.
        /// </summary>
        [Test]
        public void TheSpinNeverTurnsTheBodysOwnFacing()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            _attackerObject.transform.rotation = Quaternion.Euler(0.0f, 37.0f, 0.0f);
            Quaternion before = _attackerObject.transform.rotation;

            Assert.IsTrue(_attacker.TrySpinAttack());
            Step(_attacker.SpinAttackSeconds * 0.5f);
            Assert.Less(Quaternion.Angle(before, _attackerObject.transform.rotation), 0.5f, "the spin turned the body's facing halfway through");
            Step(_attacker.SpinAttackSeconds * 0.5f + 0.05f);
            Assert.Less(Quaternion.Angle(before, _attackerObject.transform.rotation), 0.5f, "the spin left the body turned");
        }

        /// <summary>Holding the sword out wide: the sword is a long reach, so a target 2.6 m away (well past a punch) is still hit.</summary>
        [Test]
        public void TheBladeReachesFurtherThanAPunch()
        {
            _attacker.EquippedWeapon = new FakeSwordWeapon();
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 2.6f);
            Physics.SyncTransforms();
            Assert.Greater(2.6f, _attacker.Range, "setup: the target should be out of an ordinary swing's reach");

            Assert.IsTrue(_attacker.TrySpinAttack());
            Step(_attacker.SpinAttackSeconds + 0.05f);
            Assert.Greater(_dummy.TimesHit, 0, "the blade did not reach it");
        }

        [Test]
        public void ADeadAttackerCannotSpin()
        {
            var vitals = _attackerObject.AddComponent<PlayerVitals>();
            vitals.Revive();
            Object.DestroyImmediate(_attacker);
            _attacker = _attackerObject.AddComponent<MeleeAttacker>();
            _attacker.EquippedWeapon = new FakeSwordWeapon();

            vitals.TakeDamage(10000.0f);
            Assert.IsTrue(vitals.IsDead);
            Assert.IsFalse(_attacker.TrySpinAttack(), "a corpse spun with its sword");
        }

        /// <summary>
        /// Bug report, root-caused directly with the spin attack's
        /// own diagnostic render: an Animator that has never actually
        /// evaluated a single real frame yet sits at the raw imported BIND
        /// POSE, same root cause GrappleSwing.Attach/DevConsoleView.Open
        /// already fixed for the identical reason -- spinning the instant
        /// control is gained (equip the sword, press 2 immediately) is a
        /// completely normal thing to try, not an edge case. Checked here
        /// with the REAL Player.prefab in REAL Play Mode -- the diagnostic
        /// render that found this uses a pure editor batch script instead,
        /// which (confirmed separately this same session, see
        /// PoseDistortionWatchdog's own commit and its Time.deltaTime bug)
        /// does not always behave the same as a genuine Play Mode frame for
        /// this class of Animator warm-up question, so a real PlayMode test
        /// is what actually proves or disproves this for the game as
        /// players experience it.
        /// </summary>
        [UnityTest]
        public IEnumerator SpinningTheInstantAfterSpawnDoesNotFreezeARawBindPose()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            GameObject player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 20.0f, 0.0f), Quaternion.identity);
            Animator animator = player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator, "Player.prefab has no Animator");
            Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

            try
            {
                MeleeAttacker melee = player.GetComponent<MeleeAttacker>();
                Assert.IsNotNull(melee, "Player.prefab has no MeleeAttacker");
                melee.EquippedWeapon = new FakeSwordWeapon();

                // NO yield HERE -- zero real frames, zero real Animator
                // evaluations, matching GrappleSwingPosePlayTests' own
                // AttachingTheInstantAfterSpawnDoesNotCaptureTheRawBindPose
                // exactly: fire the instant the rig exists.
                Assert.IsTrue(melee.TrySpinAttack(), "setup failed -- could not start the spin");

                float armDeviation = Quaternion.Angle(Quaternion.identity, rightUpperArm.localRotation) +
                                      Quaternion.Angle(Quaternion.identity, leftUpperArm.localRotation);
                Assert.Greater(armDeviation, 1.0f,
                    "both arms are still at the raw all-zero bind pose immediately after starting a spin attack");
            }
            finally
            {
                Object.Destroy(player);
            }
            yield break;
#else
            yield break;
#endif
        }
    }
}
