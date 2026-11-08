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

            yield return Frames(CrimsonClaw.ThrownLimpSeconds + Ragdoll.StandUpPaceSeconds + 1.0f, () => !ragdoll.IsLimp);
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

        [UnityTest]
        public IEnumerator KilledWhileHoldingItDropsThePlayer()
        {
            var ragdoll = _player.GetComponent<Ragdoll>();
            yield return UntilHolding();
            Assert.IsTrue(_claw.IsHolding, "it never caught the player");
            ((IDamageable)_claw).ApplyHit(CrimsonClaw.MaxHealth, _claw.transform.position, 0.0f);
            Assert.IsFalse(_claw.IsHolding);
            Assert.IsFalse(ragdoll.IsHeld, "its body still holds the player");
            yield return Frames(CrimsonClaw.ThrownLimpSeconds + Ragdoll.StandUpPaceSeconds + 1.0f, () => !ragdoll.IsLimp);
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
