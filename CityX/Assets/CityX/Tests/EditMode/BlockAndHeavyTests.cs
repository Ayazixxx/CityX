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
        /// A haymaker hits half again as hard as a jab. That is what is being
        /// bought with the longer wind-up.
        /// </summary>
        [Test]
        public void TheHaymakerHitsHarderThanAnyJab()
        {
            float heavy = CombatTuning.DamageFor(CombatTuning.HeavyPunch, 0);

            ComboStage[] chain = ComboChain.For(ComboChain.Unarmed);
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i].IsFinisher)
                {
                    continue;   // the finisher is meant to be the biggest hit
                }
                Assert.Greater(heavy, CombatTuning.DamageFor(chain[i], 0),
                    $"the haymaker is weaker than chain stage {i}");
            }

            Assert.AreEqual(CombatTuning.PunchDamage * 1.5f, heavy, 0.001f);
        }

        /// <summary>
        /// It opens no follow-up window. Holding for a heavy punch is a
        /// decision to commit, not a free upgrade to whichever jab was coming
        /// next.
        ///
        /// It DOES share its clip with the chain's finisher, which is fine and
        /// deliberate -- the chain reuses punch_right across three stages for
        /// the same reason. What must not be shared is the stage itself: the
        /// finisher ragdolls and closes the chain, and a haymaker that inherited
        /// either of those would be a very different move.
        ///
        /// ZERO HITSTUN, NOT A DIFFERENT REACTION -- an earlier version of
        /// this test asserted the opposite ("unlike the finisher, it stuns"),
        /// on the reasoning that a hit which neither stuns nor ragdolls gives
        /// the target nothing to react to. Checked directly against
        /// player.gd instead of trusting that reasoning: `_punch()` seeds
        /// `var hitstun := 0.0` and only overwrites it from
        /// get_last_attack_combo_info() `if not is_heavy` -- a heavy punch
        /// (or its sword equivalent, sword_heavy_attack, which runs through
        /// the identical is_heavy=true path) keeps that zero unconditionally.
        /// The real trade is real damage (1.5x) for real follow-up-window,
        /// full stop -- no consolation stun.
        /// </summary>
        [Test]
        public void TheHaymakerOpensNoFollowUpWindow()
        {
            Assert.AreEqual(0.0f, CombatTuning.HeavyPunch.WindowSeconds);
            Assert.IsFalse(CombatTuning.HeavyPunch.IsFinisher);
            Assert.AreEqual(0.0f, CombatTuning.HitstunFor(CombatTuning.HeavyPunch),
                "a heavy punch should deal real damage with zero hitstun, matching player.gd exactly");

            foreach (ComboStage stage in ComboChain.For(ComboChain.Unarmed))
            {
                if (stage.IsFinisher)
                {
                    Assert.AreEqual(0.0f, CombatTuning.HitstunFor(stage),
                        "the finisher has started stunning as well as ragdolling");
                }
            }
        }
    }
}
