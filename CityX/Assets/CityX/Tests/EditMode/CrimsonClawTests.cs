using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// CrimsonClaw's state machine, damage, and movement -- all pure per-
    /// frame logic driven through Tick(deltaTime) directly, the same
    /// "public Tick, separate from Update" shape WallStickTests already
    /// exercises. HoverCorrect/NpcSteering both call Physics.Raycast
    /// internally, which finds nothing in an EditMode test with no real
    /// physics scene -- both fall back to their own safe "nothing found"
    /// path (hover holds still, steering returns the desired direction
    /// unmodified), so none of that needs a PlayMode test to check the
    /// logic actually under test here.
    /// </summary>
    public sealed class CrimsonClawTests
    {
        /// <summary>
        /// EditMode tests share one scene across the whole run with no
        /// reload between them -- a GameObject created in one test and never
        /// destroyed is still there for the next one to trip over. Confirmed
        /// live: a leftover Player from an earlier test was the FIRST result
        /// FindFirstObjectByType&lt;PlayerVitals&gt; returned in a later one,
        /// silently substituting a stale, wrongly-positioned target. Every
        /// root this file creates -- including the new Rigidbody debris
        /// Die() spawns, which are orphaned scene roots with no parent to
        /// clean them up via -- is tracked here and destroyed in TearDown.
        /// </summary>
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();

            foreach (GameObject debris in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (debris != null && debris.name == "CrimsonClawDebris")
                {
                    Object.DestroyImmediate(debris);
                }
            }
        }

        private CrimsonClaw CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot)
        {
            var go = new GameObject("CrimsonClaw");
            _spawned.Add(go);
            leftPivot = new GameObject("LeftClawPivot").transform;
            leftPivot.SetParent(go.transform);
            rightPivot = new GameObject("RightClawPivot").transform;
            rightPivot.SetParent(go.transform);

            CrimsonClaw claw = go.AddComponent<CrimsonClaw>();
            claw.LeftClawPivot = leftPivot;
            claw.RightClawPivot = rightPivot;
            return claw;
        }

        private PlayerVitals CreatePlayer(Vector3 position)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            go.transform.position = position;
            return go.AddComponent<PlayerVitals>();
        }

        /// <summary>Ticks through a full windup+swing+recover cycle, in small steps so CheckClawHit gets several chances to see the target, matching how a real Update loop would call it every frame.</summary>
        private static void TickSeconds(CrimsonClaw claw, float seconds, float step = 0.02f)
        {
            for (float elapsed = 0.0f; elapsed < seconds; elapsed += step)
            {
                claw.Tick(step);
            }
        }

        [Test]
        public void StartsAtFullHealthAndAlive()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            Assert.IsTrue(claw.IsAlive);
            Assert.AreEqual(CrimsonClaw.MaxHealth, claw.Health, 0.001f);
        }

        [Test]
        public void TakingDamageReducesHealthAndReportsLanded()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            HitResponse response = ((IDamageable)claw).ApplyHit(40.0f, Vector3.zero, 0.0f);
            Assert.AreEqual(HitResponse.Landed, response);
            Assert.AreEqual(CrimsonClaw.MaxHealth - 40.0f, claw.Health, 0.001f);
            Assert.IsTrue(claw.IsAlive);
        }

        [Test]
        public void LethalDamageKillsItAndFurtherHitsAreIgnored()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            HitResponse killingBlow = ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth + 50.0f, Vector3.zero, 0.0f);
            Assert.AreEqual(HitResponse.Landed, killingBlow);
            Assert.IsFalse(claw.IsAlive);
            Assert.AreEqual(0.0f, claw.Health, 0.001f);

            HitResponse secondHit = ((IDamageable)claw).ApplyHit(10.0f, Vector3.zero, 0.0f);
            Assert.AreEqual(HitResponse.Ignored, secondHit);
            Assert.AreEqual(0.0f, claw.Health, 0.001f);
        }

        [Test]
        public void DyingDetachesBothClawPivotsOntoTheirOwnRigidbodies()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot);
            Transform root = claw.transform;

            ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth, Vector3.zero, 0.0f);

            Assert.AreNotEqual(root, leftPivot.parent, "left claw never detached from the corpse root");
            Assert.AreNotEqual(root, rightPivot.parent, "right claw never detached from the corpse root");
            Assert.IsNotNull(leftPivot.GetComponentInParent<Rigidbody>(), "left claw debris has no Rigidbody");
            Assert.IsNotNull(rightPivot.GetComponentInParent<Rigidbody>(), "right claw debris has no Rigidbody");
            Assert.AreNotSame(
                leftPivot.GetComponentInParent<Rigidbody>(), rightPivot.GetComponentInParent<Rigidbody>(),
                "both claws landed on the SAME debris body -- each should be independent");
        }

        [Test]
        public void DyingDisablesTheMainCollision()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            claw.MainCollision = claw.gameObject.AddComponent<BoxCollider>();

            ((IDamageable)claw).ApplyHit(CrimsonClaw.MaxHealth, Vector3.zero, 0.0f);

            Assert.IsFalse(claw.MainCollision.enabled);
        }

        [Test]
        public void NoNearbyTargetNeverStartsAnAttack()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out _);
            // Alone in the scene -- FindFirstObjectByType<PlayerVitals> finds nothing.
            TickSeconds(claw, 2.0f);
            Assert.AreEqual(Quaternion.identity, leftPivot.localRotation,
                "a claw pivot moved with no target anywhere to chase or attack");
        }

        [Test]
        public void ATargetBeyondAttackRangeIsChasedButNeverAttacked()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out Transform leftPivot, out Transform rightPivot);
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 10.0f)); // within DetectionRange, past AttackRange
            Vector3 start = claw.transform.position;

            TickSeconds(claw, 1.0f);

            Assert.Greater(Vector3.Distance(claw.transform.position, start), 0.0f, "never moved toward a reachable target");
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, player.Health, 0.001f, "took damage despite never entering attack range");
            Assert.AreEqual(Quaternion.identity, leftPivot.localRotation);
            Assert.AreEqual(Quaternion.identity, rightPivot.localRotation);
        }

        [Test]
        public void ATargetFarBeyondDetectionRangeIsIgnoredEntirely()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            CreatePlayer(new Vector3(0.0f, 0.0f, CrimsonClaw.DetectionRange * 4.0f));
            Vector3 start = claw.transform.position;

            TickSeconds(claw, 1.0f);

            Assert.AreEqual(start.x, claw.transform.position.x, 0.01f, "moved toward a target far outside detection range");
            Assert.AreEqual(start.z, claw.transform.position.z, 0.01f);
        }

        [Test]
        public void AReachableTargetGetsHitExactlyOnceOverAFullAttackCycle()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            // Well within AttackRange from the very first tick, so it attacks in place.
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 1.0f));

            TickSeconds(claw, CrimsonClaw.WindupSeconds + CrimsonClaw.SwingSeconds + CrimsonClaw.RecoverSeconds + 0.1f);

            Assert.AreEqual(PlayerVitals.BaseMaxHealth - CrimsonClaw.AttackDamage, player.Health, 0.001f,
                "the swing should have landed exactly one hit for AttackDamage");
        }

        [Test]
        public void AttackCooldownBlocksAnImmediateSecondSwing()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            PlayerVitals player = CreatePlayer(new Vector3(0.0f, 0.0f, 1.0f));

            float oneFullCycle = CrimsonClaw.WindupSeconds + CrimsonClaw.SwingSeconds + CrimsonClaw.RecoverSeconds;
            TickSeconds(claw, oneFullCycle + 0.05f);
            float healthAfterFirstAttack = player.Health;
            Assert.AreEqual(PlayerVitals.BaseMaxHealth - CrimsonClaw.AttackDamage, healthAfterFirstAttack, 0.001f);

            // Comfortably inside the cooldown window -- no second swing should even start.
            TickSeconds(claw, CrimsonClaw.AttackCooldownSeconds * 0.5f);
            Assert.AreEqual(healthAfterFirstAttack, player.Health, 0.001f,
                "a second swing landed before AttackCooldownSeconds elapsed");
        }

        [Test]
        public void SuccessiveAttacksAlternateHands()
        {
            CrimsonClaw claw = CreateCrimsonClaw(out _, out _);
            CreatePlayer(new Vector3(0.0f, 0.0f, 1.0f));
            bool startedLeft = claw.NextAttackHandIsLeft;

            // Margin bigger than AttackCooldownBlocksAnImmediateSecondSwing's
            // own +0.05f -- that test only needs to reach partway through
            // Swing to see damage land, but this one needs the FULL windup
            // -> swing -> recover -> idle transition, which can lose up to
            // one tick step's worth of overshoot at EACH of 3 phase
            // boundaries (TickAttack resets the timer to exactly 0 on
            // transition rather than carrying the overshoot forward) --
            // worst case ~3 * TickSeconds' own step, comfortably covered here.
            float oneFullCycle = CrimsonClaw.WindupSeconds + CrimsonClaw.SwingSeconds + CrimsonClaw.RecoverSeconds;
            TickSeconds(claw, oneFullCycle + 0.15f);

            Assert.AreNotEqual(startedLeft, claw.NextAttackHandIsLeft, "the next attack should swing the OTHER hand");
        }
    }
}
