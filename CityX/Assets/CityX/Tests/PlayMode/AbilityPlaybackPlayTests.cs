using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// What another player SEES when one of your abilities is shown on their copy of your body (PlayRemote): the projectile, the aura, the arm and
    /// the swoosh -- and that none of it hurts anyone. A plain body stands in for the remote copy (no network); the real sockets are
    /// NetworkedAbilitySyncPlayTests.
    /// </summary>
    public sealed class AbilityPlaybackPlayTests
    {
        private sealed class Victim : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public int Hits;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                return HitResponse.Landed;
            }
        }

        private GameObject _ground;
        private GameObject _body;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_body != null) Object.Destroy(_body);
            if (_ground != null) Object.Destroy(_ground);
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            foreach (FireballProjectile f in Object.FindObjectsByType<FireballProjectile>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            foreach (IceShardProjectile s in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None)) Object.Destroy(s.gameObject);
            foreach (SwordWaveProjectile w in Object.FindObjectsByType<SwordWaveProjectile>(FindObjectsSortMode.None)) Object.Destroy(w.gameObject);
        }

#if UNITY_EDITOR
        /// <summary>A plain player body (Player.prefab) with the three casters on it, standing in for another player's copy of a body.</summary>
        private GameObject RemoteBody()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            GameObject body = Object.Instantiate(prefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            body.AddComponent<FireballCaster>().FireballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            body.AddComponent<IceShardCaster>().ShardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/IceShard.prefab");
            body.AddComponent<SwordWaveCaster>().WavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SwordWave.prefab");
            _body = body;
            return body;
        }

        private Victim VictimAt(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = position;
            _made.Add(go);
            Physics.SyncTransforms();
            return go.AddComponent<Victim>();
        }
#endif

        [UnityTest]
        public IEnumerator AnotherPlayersFireballFliesAndBurstsButHurtsNothing()
        {
#if UNITY_EDITOR
            GameObject body = RemoteBody();
            Victim victim = VictimAt(new Vector3(0.0f, 1.2f, 7.0f));
            var fireball = body.GetComponent<FireballCaster>();
            int announced = 0;
            fireball.Fired += _ => announced++;

            fireball.PlayRemote(new AbilityEvent(0, new Vector3(0.0f, 1.2f, 0.6f), Vector3.forward, FireballCaster.FireballSpeed));
            FireballProjectile shown = Object.FindFirstObjectByType<FireballProjectile>();
            Assert.IsNotNull(shown, "no fireball appeared");
            Assert.IsTrue(shown.Cosmetic, "the copy of someone else's fireball must be cosmetic");

            for (int i = 0; i < 200 && shown != null; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(shown == null, "the fireball never reached anything");
            Assert.AreEqual(0, victim.Hits, "another player's fireball, shown on this machine, hurt something");
            Assert.AreEqual(0, announced, "showing it announced it again");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersIceShardSticksInTheTargetAndHurtsNothing()
        {
#if UNITY_EDITOR
            GameObject body = RemoteBody();
            Victim victim = VictimAt(new Vector3(0.0f, 1.2f, 7.0f));
            var ice = body.GetComponent<IceShardCaster>();

            ice.PlayRemote(new AbilityEvent(0, new Vector3(0.0f, 1.2f, 0.6f), Vector3.forward, ice.ShardSpeed, 0.7f, 0));
            IceShardProjectile shard = Object.FindFirstObjectByType<IceShardProjectile>();
            Assert.IsNotNull(shard, "no shard appeared");
            Assert.IsTrue(shard.Cosmetic);

            for (int i = 0; i < 2000 && shard.CurrentPhase == IceShardProjectile.Phase.Flying; i++)
            {
                yield return null;
            }
            Assert.AreEqual(IceShardProjectile.Phase.StuckInCharacter, shard.CurrentPhase, "it should stick in what it meets, like the real one");
            Assert.AreEqual(0, victim.Hits, "another player's shard, shown on this machine, hurt something");
            Assert.AreEqual(IceShardProjectile.CharacterStickSeconds, shard.StuckSecondsRemaining, 0.5f, "and it ages on the same clock");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCrescentFliesThroughATargetWithoutHurtingIt()
        {
#if UNITY_EDITOR
            GameObject body = RemoteBody();
            Victim victim = VictimAt(new Vector3(0.0f, 1.8f, 7.0f));
            var wave = body.GetComponent<SwordWaveCaster>();

            wave.PlayRemote(new AbilityEvent(0, new Vector3(0.0f, 1.8f, 0.9f), Vector3.forward, SwordWaveCaster.WaveSpeed));
            SwordWaveProjectile shown = Object.FindFirstObjectByType<SwordWaveProjectile>();
            Assert.IsNotNull(shown, "no crescent appeared");
            Assert.IsTrue(shown.Cosmetic);

            for (int i = 0; i < 100; i++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(0, victim.Hits, "another player's crescent, shown on this machine, hurt something");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCastRaisesTheHandOnTheirBodyAndLowersItAgain()
        {
#if UNITY_EDITOR
            GameObject body = RemoteBody();
            body.GetComponent<FireballCaster>().PlayRemote(new AbilityEvent(0, Vector3.up, Vector3.forward, 20.0f));
            var pose = body.GetComponent<RemoteAbilityPose>();
            Assert.IsNotNull(pose, "the hand was never raised");
            yield return null;
            Assert.Greater(pose.Fraction, 0.5f, "the hand should be up right after the cast");

            pose.Step(5.0f);
            Assert.AreEqual(0.0f, pose.Fraction, 1e-4f, "and down again after");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersBloodrageShowsTheAuraAndTheAnimationsThenPutsThemBack()
        {
#if UNITY_EDITOR
            GameObject body = RemoteBody();
            var rage = body.GetComponent<BloodrageAbility>();
            Assert.IsNotNull(rage);
            int announced = 0;
            rage.Fired += _ => announced++;

            rage.PlayRemote(new AbilityEvent(0, Vector3.zero, Vector3.zero, BloodrageAbility.DurationSeconds));
            yield return null;
            Assert.IsTrue(rage.IsActive, "their Bloodrage was not shown");
            Assert.IsNotNull(body.transform.Find("BloodrageAura(Clone)"), "no red aura on their body");

            rage.Tick(BloodrageAbility.DurationSeconds + 0.1f);
            Assert.IsFalse(rage.IsActive, "it should end on its own clock");
            rage.Tick(BloodrageAbility.AuraFadeOutSeconds + 0.2f);
            yield return null;
            Assert.IsNull(body.transform.Find("BloodrageAura(Clone)"), "the aura never faded away");
            Assert.AreEqual(0, announced, "showing it announced it again");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersSwordSpinShowsTheArmOutAndTheSwooshThenEnds()
        {
#if UNITY_EDITOR
            GameObject body = RemoteBody();
            var melee = body.GetComponent<MeleeAttacker>();
            var locomotion = body.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(melee.SpinTrailPrefab, "setup: the player has no swoosh prefab");
            yield return null;

            melee.PlayRemote(new AbilityEvent(MeleeAttacker.SpinEventTurning, Vector3.zero, Vector3.forward, 0.0f, 0.0f));
            Assert.IsTrue(melee.IsShowingRemoteSpin);
            // Half a second of real frames: the arm eases out at a fixed rate per second, not per frame.
            for (float t = 0.0f; t < 0.5f; t += Time.deltaTime)
            {
                melee.TickRemoteSpin(Time.deltaTime);
                yield return null;
            }
            Assert.Greater(locomotion.SwordSpinPoseBlend, 0.5f, "the sword arm never went out");
            Transform trail = body.transform.Find("SwordSpinTrail");
            Assert.IsNotNull(trail, "no swoosh");
            Assert.Greater(trail.GetComponent<SwordSpinTrail>().Intensity, 0.3f, "the swoosh is not showing");

            melee.PlayRemote(new AbilityEvent(MeleeAttacker.SpinEventEnded, Vector3.zero, Vector3.forward));
            Assert.IsFalse(melee.IsShowingRemoteSpin);
            for (float t = 0.0f; t < 0.6f; t += Time.deltaTime)
            {
                yield return null;
            }
            Assert.Less(locomotion.SwordSpinPoseBlend, 0.05f, "the arm stayed out after the spin ended");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ALostEndEventCannotStrandTheSpinPose()
        {
#if UNITY_EDITOR
            GameObject body = RemoteBody();
            var melee = body.GetComponent<MeleeAttacker>();
            yield return null;
            melee.PlayRemote(new AbilityEvent(MeleeAttacker.SpinEventCharging, Vector3.zero, Vector3.forward));
            melee.TickRemoteSpin(0.5f);
            Assert.IsTrue(melee.IsShowingRemoteSpin, "charging should hold the pose");
            melee.TickRemoteSpin(5.0f);
            Assert.IsFalse(melee.IsShowingRemoteSpin, "a charge whose end packet never arrived must time out, not stay up for ever");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheSyncFindsAnAbilityByItsHashAndIgnoresStrangers()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            GameObject body = RemoteBody();
            var sync = body.AddComponent<NetworkedAbilitySync>();
            body.GetComponent<FishNet.Object.NetworkObject>().SetIsNetworked(false);
            sync.Gather();
            yield return null;

            var evt = new AbilityEvent(0, new Vector3(0.0f, 1.2f, 0.6f), Vector3.forward, 20.0f);
            Assert.IsTrue(sync.Replay(AbilityIds.Hash(FireballCaster.Id), evt), "the fireball was not found by its hash");
            Assert.IsNotNull(Object.FindFirstObjectByType<FireballProjectile>(), "and nothing was shown");
            Assert.IsFalse(sync.Replay(AbilityIds.Hash("no-such-ability"), evt), "an unknown ability must be ignored");
            LogAssert.ignoreFailingMessages = false;
#else
            yield break;
#endif
        }
    }
}
