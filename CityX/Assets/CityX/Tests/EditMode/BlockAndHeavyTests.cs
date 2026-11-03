using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Blocking and the haymaker -- the two combat options that make a fight
    /// something other than mashing one button.
    /// </summary>
    public sealed class BlockAndHeavyTests
    {
        private GameObject _object;
        private PlayerVitals _vitals;
        private IDamageable _target;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Body");
            _vitals = _object.AddComponent<PlayerVitals>();
            // Everyone's 10% chance to stop a blow outright (Block Chance, 2026-10-14) would make these blocks random: never here.
            _vitals.Roll = () => 1.0f;
            // EditMode never runs Awake, so the health starts at zero and every
            // test would be hitting an already-dead body. Revive does the same
            // filling-up and is already public for respawn.
            _vitals.Revive();
            _target = _vitals;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void ABlockLetsOnlyAQuarterThrough()
        {
            float full = _vitals.MaxHealth;

            _vitals.StartBlock();
            HitResponse response = _target.ApplyHit(40.0f, Vector3.zero, 0.5f);

            Assert.AreEqual(HitResponse.Blocked, response);
            Assert.AreEqual(full - 10.0f, _vitals.Health, 0.01f);
        }

        /// <summary>
        /// A blocked hit must not also stun the defender. Taking the hitstun as
        /// well as a quarter of the damage would make blocking a worse dodge
        /// rather than an opening -- and the opening is the entire reason to
        /// time one.
        /// </summary>
        [Test]
        public void ABlockedHitDoesNotStunTheDefender()
        {
            _vitals.StartBlock();
            _target.ApplyHit(10.0f, Vector3.zero, 0.5f);

            Assert.IsFalse(_vitals.IsStunned, "blocking still ate the hitstun");
        }

        [Test]
        public void AnUnblockedHitStunsAsBefore()
        {
            _target.ApplyHit(10.0f, Vector3.zero, 0.5f);

            Assert.IsTrue(_vitals.IsStunned);
        }

        /// <summary>
        /// The window is exactly as long as the clip and cannot be extended.
        /// Godot is explicit: you cannot hold a block, you have to time it.
        /// </summary>
        [Test]
        public void ABlockRunsOutOnItsOwn()
        {
            _vitals.StartBlock();
            Assert.IsTrue(_vitals.IsBlocking);

            _vitals.TickHitstun(PlayerVitals.BlockWindowSeconds + 0.01f);

            Assert.IsFalse(_vitals.IsBlocking);
            Assert.AreEqual(HitResponse.Landed, _target.ApplyHit(10.0f, Vector3.zero, 0.5f));
        }

        /// <summary>
        /// Dashing through a punch takes nothing at all -- which is different
        /// from blocking it, and has to report differently, or the attacker
        /// gets punished for a hit the defender simply avoided.
        /// </summary>
        [Test]
        public void DashingThroughAHitIsIgnoredRatherThanBlocked()
        {
            float full = _vitals.Health;
            _vitals.GrantInvulnerability(PlayerLocomotion.DashSeconds);

            HitResponse response = _target.ApplyHit(40.0f, Vector3.zero, 0.5f);

            Assert.AreEqual(HitResponse.Ignored, response);
            Assert.AreEqual(full, _vitals.Health, 0.01f);
        }

        [Test]
        public void InvulnerabilityRunsOut()
        {
            _vitals.GrantInvulnerability(PlayerLocomotion.DashSeconds);
            _vitals.TickHitstun(PlayerLocomotion.DashSeconds + 0.01f);

            Assert.IsFalse(_vitals.IsInvulnerable);
            Assert.AreEqual(HitResponse.Landed, _target.ApplyHit(10.0f, Vector3.zero, 0.5f));
        }

        /// <summary>
        /// The overhand hits twice as hard as a straight. That is what is being
        /// bought with the longer wind-up (and the stamina).
        /// </summary>
        [Test]
        public void TheOverhandHitsHarderThanAnyStraight()
        {
            float heavy = CombatTuning.DamageFor(CombatTuning.HeavyPunch, 0);

            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            for (int i = 0; i < chain.Length; i++)
            {
                Assert.Greater(heavy, CombatTuning.DamageFor(chain[i], 0),
                    $"the overhand is weaker than chain stage {i}");
            }
            Assert.Greater(CombatTuning.DamageFor(CombatTuning.UppercutPunch, 0), CombatTuning.DamageFor(chain[0], 0), "the uppercut is weaker than a straight");
            Assert.Greater(heavy, CombatTuning.DamageFor(CombatTuning.UppercutPunch, 0), "the uppercut out-hits the overhand");
        }

        /// <summary>
        /// The overhand is outside the chain: a light after it starts the chain over, and its window is only for the held uppercut, which opens
        /// nothing. Holding for a heavy punch is a decision to commit, not a free upgrade to whichever straight was coming next.
        ///
        /// ZERO HITSTUN, NOT A DIFFERENT REACTION: the overhand knocks the target down (the ragdoll), so a stun on top would be a second
        /// competing reaction to the same hit. The uppercut does not knock down, so it stuns, longest of the fists.
        /// </summary>
        [Test]
        public void TheOverhandKnocksDownAndOnlyTheUppercutFollowsIt()
        {
            Assert.IsFalse(CombatTuning.HeavyPunch.IsFinisher);
            Assert.IsTrue(CombatTuning.HeavyPunch.KnocksDown);
            Assert.Greater(CombatTuning.HeavyPunch.WindowSeconds, 0.0f, "the overhand leaves no time for the held uppercut");
            Assert.AreEqual(0.0f, CombatTuning.HitstunFor(CombatTuning.HeavyPunch),
                "a knockdown blow should deal real damage with zero hitstun");

            Assert.AreEqual(0.0f, CombatTuning.UppercutPunch.WindowSeconds, "the uppercut opens a window of its own");
            Assert.IsFalse(CombatTuning.UppercutPunch.KnocksDown);
            foreach (ComboStage stage in ComboChain.For(ComboChain.Unarmed))
            {
                Assert.Greater(CombatTuning.HitstunFor(CombatTuning.UppercutPunch), CombatTuning.HitstunFor(stage), "the uppercut stuns less than a straight");
            }
        }
    }
}
