using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Crimson Claw's one-handed grab and throw on a real player body (owner, 2026-11-08: "can you make an attack where the crimson claw
    /// grabs you and throws you", then "i wanted it to be a 1 handed grab"): its swipe catches the player in front of it, holds them limp up
    /// in that claw, flings them ahead of it, hurt, and they get up again; a dash's dodge at the catch slips it; killed while holding, it drops
    /// them.
    /// </summary>
    public sealed class CrimsonClawGrabPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private CrimsonClaw _claw;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 2.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _player.GetComponent<PlayerVitals>().Revive();
            _claw = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/CrimsonClaw.prefab"),
                new Vector3(0.0f, CrimsonClaw.HoverHeightAboveGround, 0.0f), Quaternion.identity).GetComponent<CrimsonClaw>();
            _claw.GrabNext();   // here: a few frames pass before the test begins, and it attacks in the first
            Physics.SyncTransforms();
#else
            Assert.Ignore("needs the editor to load the prefabs.");
#endif
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in new[] { _ground, _player, _claw != null ? _claw.gameObject : null })
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
            foreach (GameObject debris in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (debris.name == "CrimsonClawDebris")
                {
                    Object.Destroy(debris);
                }
            }
        }

        [UnityTest]
        public IEnumerator ItCatchesHoldsAndThrowsThePlayerInFrontOfIt()
        {
            var vitals = _player.GetComponent<PlayerVitals>();
            var ragdoll = _player.GetComponent<Ragdoll>();
            yield return UntilHolding();
            Assert.IsTrue(_claw.IsHolding, "it never caught the player in front of it");
            Assert.IsTrue(ragdoll.IsLimp && ragdoll.IsHeld, "caught, the player is not held limp");
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, vitals.Health, 0.001f, "the player was hurt before it caught them");

            // Held stiff and upright in its fist (the owner's drawing) while it raises its arm.
            yield return Frames(0.4f);
            Assert.Less(Vector3.Distance(ragdoll.TorsoMiddle, _claw.FistPoint()), 0.05f, "the player is not held in its fist");
            Assert.Less(Vector3.Angle(_player.transform.up, Vector3.up), 1.0f, "the player is not held upright");
            Assert.IsTrue(ragdoll.Torso.isKinematic, "held, the player is floppy, not stiff in its fist");
            Assert.Greater(_claw.transform.InverseTransformPoint(ragdoll.TorsoMiddle).z, 1.0f, "the player is not held in front of it");
            Assert.Greater(_player.transform.position.y, 0.2f, "the player's feet are not off the ground");
            Assert.AreEqual(PlayerVitals.BaseMaxHealth, vitals.Health, 0.001f, "caught, the player was hurt before the throw");

            Assert.IsTrue(ragdoll.IsLimp && ragdoll.IsHeld, "the player got up while held");
            yield return Frames(CrimsonClaw.GrabSeconds, () => !_claw.IsHolding);
            Assert.IsFalse(_claw.IsHolding, "it never let go");
            Assert.IsFalse(ragdoll.IsHeld, "thrown, the player is still held");
            Assert.IsFalse(ragdoll.Torso.isKinematic, "thrown, the player is not limp");
            Assert.AreEqual(PlayerVitals.BaseMaxHealth - CrimsonClaw.ThrowDamage, vitals.Health, 0.001f, "the throw did not hurt");
            Vector3 thrownFrom = ragdoll.Torso.position;
            yield return Frames(0.5f);
            Vector3 flew = ragdoll.Torso.position - thrownFrom;
            Assert.Greater(Vector3.Dot(flew, _claw.transform.forward), 2.5f, "the player was not thrown ahead of it");

            yield return Frames(CrimsonClaw.ThrownLimpSeconds + Ragdoll.StandUpRecoverySeconds + 1.0f, () => !ragdoll.IsLimp);
            Assert.IsFalse(ragdoll.IsLimp, "thrown, the player never got up");
        }

        [UnityTest]
        public IEnumerator ADashAtTheCatchSlipsIt()
        {
            var vitals = _player.GetComponent<PlayerVitals>();
            yield return Frames(3.0f, () => _claw.IsAttacking);
            Assert.IsTrue(_claw.IsAttacking, "it never went for the player");
            vitals.GrantInvulnerability(CrimsonClaw.GrabSeconds);   // the dash's dodge, held through the whole grab
            bool caught = false;
            yield return Frames(CrimsonClaw.GrabSeconds + 0.2f, () =>
            {
                caught |= _claw.IsHolding;
                return !_claw.IsAttacking;
            });
            Assert.IsFalse(caught, "it caught a player dashing");
            Assert.IsFalse(_player.GetComponent<Ragdoll>().IsLimp, "a player who dashed was thrown down");
        }

        /// <summary>Owner, 2026-11-08: "make the crimson claw grab blockable". The guard takes it as a blow, and letting it down after does not get you caught.</summary>
        [UnityTest]
        public IEnumerator AGuardStopsTheGrab()
        {
            var vitals = _player.GetComponent<PlayerVitals>();
            var ragdoll = _player.GetComponent<Ragdoll>();
            bool caught = false;
            bool attacked = false;
            yield return Frames(3.0f + CrimsonClaw.GrabSeconds, () =>
            {
                caught |= _claw.IsHolding;
                attacked |= _claw.IsAttacking;
                if (vitals.Health >= PlayerVitals.BaseMaxHealth - 0.001f)
                {
                    vitals.StartBlock();   // up until the blow lands, then let down: the rest of the swing must not catch
                }
                return attacked && !_claw.IsAttacking;
            });
            Assert.IsTrue(attacked, "setup: it never went for the player");
            Assert.IsFalse(caught, "it caught a player who was guarding");
            Assert.IsFalse(ragdoll.IsLimp, "a player who guarded was thrown down");
            Assert.Greater(vitals.Health, PlayerVitals.BaseMaxHealth - CrimsonClaw.AttackDamage, "the guard stopped none of the blow");
        }

        [UnityTest]
        public IEnumerator ARaisedShieldStopsTheGrabFromInFrontOnly()
        {
            var vitals = _player.GetComponent<PlayerVitals>();
            var shield = new GameObject("Shield");
            shield.transform.SetParent(_player.transform, false);
            vitals.Shield = shield.transform;
            vitals.ShieldRaised = true;
            bool caught = false;
            bool attacked = false;
            yield return Frames(3.0f + CrimsonClaw.GrabSeconds, () =>
            {
                caught |= _claw.IsHolding;
                attacked |= _claw.IsAttacking;
                return attacked && !_claw.IsAttacking;
            });
            Assert.IsTrue(attacked, "setup: it never went for the player");
            Assert.IsFalse(caught, "it caught a player behind a raised shield");

            _claw.GrabNext();
            _player.transform.rotation = Quaternion.identity;   // its back to the claw: the shield covers nothing there
            Physics.SyncTransforms();
            yield return UntilHolding();
            Assert.IsTrue(_claw.IsHolding, "a shield held the other way should not stop it");
        }

        [Test]
        public void TheGuardsAgainstABlowAreABlockAShieldInFrontAndABarrier()
        {
            var vitals = _player.GetComponent<PlayerVitals>();
            Vector3 ahead = _player.transform.position + _player.transform.forward * 3.0f;
            Vector3 behind = _player.transform.position - _player.transform.forward * 3.0f;
            Assert.IsFalse(vitals.GuardsAgainst(ahead), "nothing is up yet");

            vitals.StartBlock();
            Assert.IsTrue(vitals.GuardsAgainst(behind), "a block is a guard");
            vitals.TickHitstun(PlayerVitals.BlockWindowSeconds + 0.1f);
            Assert.IsFalse(vitals.GuardsAgainst(ahead), "setup: the block ran out");

            var shield = new GameObject("Shield");
            shield.transform.SetParent(_player.transform, false);
            vitals.Shield = shield.transform;
            vitals.ShieldRaised = true;
            Assert.IsTrue(vitals.GuardsAgainst(ahead), "a shield guards the front");
            Assert.IsFalse(vitals.GuardsAgainst(behind), "and not the back");
            vitals.ShieldRaised = false;

            vitals.Barrier = new FrontBarrier(_player.transform);
            Assert.IsTrue(vitals.GuardsAgainst(ahead), "a barrier guards what it covers");
            Assert.IsFalse(vitals.GuardsAgainst(behind), "and not what it does not");
            vitals.Barrier = null;
        }

        private sealed class FrontBarrier : IBarrier
        {
            private readonly Transform _body;

            public FrontBarrier(Transform body)
            {
                _body = body;
            }

            public bool Covers(Vector3 fromPosition) => Vector3.Dot(fromPosition - _body.position, _body.forward) > 0.0f;

            public void Absorb(float amount, Vector3 fromPosition)
            {
            }
        }

        [UnityTest]
        public IEnumerator KilledWhileHoldingItDropsThePlayer()
        {
            var ragdoll = _player.GetComponent<Ragdoll>();
            yield return UntilHolding();
            Assert.IsTrue(_claw.IsHolding, "it never caught the player");
            ((IDamageable)_claw).ApplyHit(CrimsonClaw.MaxHealth, _claw.transform.position, 0.0f);
            Assert.IsFalse(_claw.IsHolding);
            Assert.IsFalse(ragdoll.IsHeld, "its body still holds the player");
            yield return Frames(CrimsonClaw.ThrownLimpSeconds + Ragdoll.StandUpRecoverySeconds + 1.0f, () => !ragdoll.IsLimp);
            Assert.IsFalse(ragdoll.IsLimp, "dropped, the player never got up");
        }

        private IEnumerator UntilHolding() => Frames(3.0f, () => _claw.IsHolding);

        /// <summary>
        /// Frames as PlayerInputReader runs them for the player (its controls are off here): its vitals' clocks (the knockdown's timer among
        /// them) and, limp and alive, its ragdoll's own tick.
        /// </summary>
        private IEnumerator Frames(float seconds, System.Func<bool> until = null)
        {
            var vitals = _player.GetComponent<PlayerVitals>();
            var ragdoll = _player.GetComponent<Ragdoll>();
            for (float t = 0.0f; t < seconds && (until == null || !until()); t += Time.deltaTime)
            {
                vitals.TickHitstun(Time.deltaTime);
                if (ragdoll.IsLimp && !vitals.IsDead)
                {
                    ragdoll.Tick(Time.deltaTime);
                }
                yield return null;
            }
        }
    }
}
