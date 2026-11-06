using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// SELF SMITE ON THE REAL RIG WITH THE REAL KEY (owner, 2026-10-15: Pikachu's Thunder, on the bar). Key 9: the body stands still, the
    /// animation plays, a bolt comes down from 1.5 km and lands at 0.55 s; whatever it passed on the way down is hurt; the burst round the
    /// caster hurts and throws those close and nobody further; the caster is untouchable as it lands and never hurt by it; free at 1.23 s.
    /// </summary>
    public sealed class SelfSmitePlayTests : InputTestFixture
    {
        /// <summary>A target that records what it took and how it was thrown.</summary>
        private sealed class Target : MonoBehaviour, IDamageable, ILaunchable
        {
            public float Damage;
            public int Hits;
            public float FirstHitAt = -1.0f;
            public Vector3 Thrown;

            public bool IsAlive => true;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Damage += amount;
                Hits++;
                if (FirstHitAt < 0.0f)
                {
                    FirstHitAt = Time.time;
                }
                return HitResponse.Landed;
            }

            public void Launch(Vector3 velocity)
            {
                Thrown = velocity;
            }
        }

        private Keyboard _keyboard;
        private GameObject _ground;
        private GameObject _player;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (SelfSmiteBolt bolt in Object.FindObjectsByType<SelfSmiteBolt>(FindObjectsSortMode.None))
            {
                Object.Destroy(bolt.gameObject);
            }
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            base.TearDown();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Until(SelfSmiteAbility smite, float seconds)
        {
            for (int i = 0; i < 600 && smite.IsActive && smite.Seconds < seconds; i++)
            {
                yield return null;
            }
        }

        private SelfSmiteAbility Stage()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            return _player.AddComponent<SelfSmiteAbility>();
#else
            return null;
#endif
        }

        private Target MakeTarget(Vector3 feet)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = feet + Vector3.up;
            _made.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<Target>();
        }

        [UnityTest]
        public IEnumerator KeyNineCallsTheBoltDownOntoTheCasterOnThundersTimeline()
        {
#if UNITY_EDITOR
            SelfSmiteAbility smite = Stage();
            var vitals = _player.GetComponent<PlayerVitals>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            yield return Frames(30);
            Target near = MakeTarget(new Vector3(2.5f, 0.0f, 0.0f));
            Target far = MakeTarget(new Vector3(9.0f, 0.0f, 0.0f));
            // Hanging high up in the bolt's way: the bolt's fall, not the burst.
            Target above = MakeTarget(new Vector3(0.6f, 30.0f, 0.0f));
            float health = vitals.Health;
            Vector3 start = _player.transform.position;

            Press(_keyboard.digit9Key);
            yield return null;
            Release(_keyboard.digit9Key);
            yield return null;
            Assert.IsTrue(smite.IsActive, "9 did not cast Self Smite (it is on key 9 out of the box)");
            yield return Frames(6);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(SelfSmiteMove.StateName) ||
                          animator.GetNextAnimatorStateInfo(0).IsName(SelfSmiteMove.StateName), "the animation is not playing");

            // The body stands still for all of it, W or not.
            Press(_keyboard.wKey);
            yield return Until(smite, 0.3f);
            Assert.IsNotNull(smite.Bolt, "no bolt");
            Assert.AreEqual(CloudLayer.HeightMetres, smite.Bolt.CloudBase, 5.0f, "the cloud should be a kilometre and a half over the ground");
            Assert.IsTrue(smite.Bolt.CloudShown);

            yield return Until(smite, SelfSmiteMove.StrikeSeconds - 0.05f);
            Assert.AreEqual(0, near.Hits, "the burst came before the bolt landed");
            Assert.IsFalse(vitals.IsInvulnerable, "the wind-up can be hit, as Thunder's can");
            Assert.AreEqual(1, above.Hits, "the bolt's fall should have hurt what hung in its way");
            Assert.AreEqual(SelfSmiteMove.BoltDamage, above.Damage, 0.01f);

            yield return Until(smite, SelfSmiteMove.StrikeSeconds + 0.02f);
            Assert.AreEqual(1, near.Hits, "the burst did not hurt somebody 2.5 m away");
            Assert.AreEqual(SelfSmiteMove.BlastDamage, near.Damage, 0.01f);
            Assert.Greater(near.Thrown.x, 5.0f, "the burst should throw them away");
            Assert.Greater(near.Thrown.y, 3.0f, "and up");
            Assert.IsTrue(vitals.IsInvulnerable, "the caster cannot be hit as the bolt lands");
            Assert.IsTrue(smite.Bolt != null && smite.Bolt.BlastShown, "no burst drawn");

            yield return Until(smite, 1.0f);
            Assert.AreEqual(0, far.Hits, "somebody 9 m away was hurt");
            Assert.AreEqual(1, near.Hits, "the burst hurts each body once");
            Assert.AreEqual(1, above.Hits);
            Assert.AreEqual(health, vitals.Health, 0.01f, "Self Smite hurt its caster");
            Assert.Less(Vector3.Distance(start, _player.transform.position), 0.1f, "the body walked during the move");

            yield return Until(smite, 2.0f);
            Assert.IsFalse(smite.IsActive, "never let go of the caster");
            yield return Frames(60);
            Release(_keyboard.wKey);
            Assert.Greater(Vector3.Distance(start, _player.transform.position), 0.8f, "free again, W should walk");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AStunBeforeTheStrikeStopsItAndTheBoltLandsWithNoBurst()
        {
#if UNITY_EDITOR
            SelfSmiteAbility smite = Stage();
            var vitals = _player.GetComponent<PlayerVitals>();
            yield return Frames(30);
            Target near = MakeTarget(new Vector3(2.5f, 0.0f, 0.0f));
            Assert.IsTrue(smite.TryCast());
            SelfSmiteBolt bolt = smite.Bolt;
            yield return Until(smite, 0.3f);
            ((IDamageable)vitals).ApplyHit(5.0f, _player.transform.position + Vector3.forward, 0.5f);
            yield return Frames(2);
            Assert.IsFalse(smite.IsActive, "a stunning blow should knock the caster out of it");
            Assert.IsFalse(_player.GetComponent<PlayerLocomotion>().MovementRooted, "left rooted");
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }
            Assert.AreEqual(0, near.Hits, "the burst went off without the caster in the move");
            Assert.IsTrue(bolt != null && bolt.Seconds > SelfSmiteMove.StrikeSeconds, "the bolt should still come down");
            Assert.IsFalse(bolt.BlastShown);
#else
            yield break;
#endif
        }

        /// <summary>Stands in for NetworkedDamage on another player's copy: records what would be sent to the real body.</summary>
        private sealed class FakeRelay : IRemoteDamageRelay
        {
            public Vector3 KnockdownSent;
            public int Knockdowns;

            public bool TryRelayHit(float amount, Vector3 fromPosition, float hitstunSeconds, float poiseDamage) => true;

            public bool TryRelayPoison(float amount) => true;

            public bool TryRelayKnockdown(Vector3 velocity)
            {
                KnockdownSent = velocity;
                Knockdowns++;
                return true;
            }

            public bool TryRelayPull(Vector3 direction, float metres, float seconds, float lift) => true;

            public bool TryRelayHeal(float amount) => true;
        }

        /// <summary>
        /// The burst throws other players for real (2026-10-15): a throw on another player's copy is sent on to their own body (before, only
        /// the attacker's copy fell, and the victim on their own machine never did), and the copy here still falls, so the attacker sees it.
        /// </summary>
        [UnityTest]
        public IEnumerator AThrowOnAnotherPlayersCopyIsSentToTheirOwnBodyAndTheCopyStillFalls()
        {
#if UNITY_EDITOR
            Stage();
            yield return Frames(30);
            var vitals = _player.GetComponent<PlayerVitals>();
            var relay = new FakeRelay();
            vitals.Relay = relay;
            Vector3 throwBy = new Vector3(9.0f, 6.0f, 0.0f);
            vitals.TriggerLaunchKnockdown(throwBy);
            Assert.AreEqual(1, relay.Knockdowns, "the throw was not sent to the real body");
            Assert.AreEqual(throwBy, relay.KnockdownSent);
            yield return null;
            Assert.IsTrue(_player.GetComponent<Ragdoll>().IsLimp, "the copy should still fall here");
            vitals.Relay = null;
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ItCannotBeCastAgainUntilTheCooldownIsOver()
        {
#if UNITY_EDITOR
            SelfSmiteAbility smite = Stage();
            yield return Frames(30);
            Assert.IsTrue(smite.TryCast());
            Assert.IsFalse(smite.TryCast(), "cast twice at once");
            yield return Until(smite, 2.0f);
            Assert.IsFalse(smite.IsActive);
            Assert.IsFalse(smite.TryCast(), "cast again inside the cooldown");
            Assert.Greater(smite.CooldownRemaining, 0.0f);
#else
            yield break;
#endif
        }
    }
}
