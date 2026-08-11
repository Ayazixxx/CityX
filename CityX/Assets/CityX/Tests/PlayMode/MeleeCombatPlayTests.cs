using CityX.Character;
using NUnit.Framework;
using UnityEngine;

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
            // Well inside the 2.5m reach, directly in front (+Z is forward at
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
            Step(_attacker.SwingSeconds + 0.01f);
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
            Assert.AreEqual(CombatTuning.PunchDamage, _dummy.TotalDamageTaken, 0.01f);
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

            Step(_attacker.SwingSeconds * 0.5f);
            Assert.IsFalse(_attacker.TryAttack(), "swing released halfway through");

            Step(_attacker.SwingSeconds);
            Assert.IsFalse(_attacker.IsSwinging, "the swing never released the body");
            Assert.IsTrue(_attacker.TryAttack(), "could not swing again after the swing finished");
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
                Step(_attacker.SwingSeconds + 0.01f);
            }

            Assert.AreEqual(chain.Length, _dummy.TimesHit, "not every stage of the chain landed");
            Assert.AreEqual(0, _attacker.NextStageIndex,
                "the chain did not reset after its finisher");
        }

        [Test]
        public void TheFinisherHitsHarderThanTheOpener()
        {
            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);

            SwingOnce();
            float first = _dummy.TotalDamageTaken;

            // Walk the rest of the chain to reach and consume the finisher, so
            // the next pass starts from a clean chain rather than mid-combo.
            for (int i = 1; i < chain.Length; i++)
            {
                Assert.IsTrue(_attacker.TryAttack());
                Step(_attacker.SwingSeconds + 0.01f);
            }

            _dummy.Reset();
            Step(2.0f);   // let the window lapse so the chain restarts

            for (int i = 0; i < chain.Length - 1; i++)
            {
                Assert.IsTrue(_attacker.TryAttack());
                Step(_attacker.SwingSeconds + 0.01f);
            }
            float beforeFinisher = _dummy.TotalDamageTaken;
            Assert.IsTrue(_attacker.TryAttack());
            Step(_attacker.SwingSeconds + 0.01f);
            float finisherDamage = _dummy.TotalDamageTaken - beforeFinisher;

            Assert.Greater(finisherDamage, first,
                $"the finisher did {finisherDamage:0.0} against an opener's {first:0.0}");
            Assert.AreEqual(0.0f, _dummy.LastHitstun, 0.001f,
                "the finisher applied hitstun as well as ragdolling");
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
            Step(_attacker.SwingSeconds + 0.01f);
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
            Step(_attacker.SwingSeconds + 0.01f);

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
            Step(_attacker.SwingSeconds + 0.01f);

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
            Assert.AreEqual(CombatTuning.PunchDamage * 2.0f, _dummy.TotalDamageTaken, 0.05f,
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
    }
}
