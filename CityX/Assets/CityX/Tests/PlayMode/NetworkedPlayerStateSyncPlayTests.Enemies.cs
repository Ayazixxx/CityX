using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using FishNet.Managing;
using FishNet.Object;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    // Enemies every player sees (owner, 2026-10-27: "do the enemy sync between players"): player A asks for an enemy, the server spawns it
    // with A as its owner, A's machine runs it and B's holds a copy; B's blows on the copy land on A's real enemy, its death fells B's copy,
    // and the XP goes to B, whose blow it was.
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        private readonly List<EnemyNetwork> _enemyNetworks = new List<EnemyNetwork>();

#if UNITY_EDITOR
        /// <summary>One EnemyNetwork per machine of the test (the server's and each client's), as the game's scene has one.</summary>
        private IEnumerator StartEnemyNetworks()
        {
            var kinds = new[]
            {
                Kind("swordsman", "Assets/CityX/Character/NetworkedSwordsman.prefab"),
                Kind("shoota", "Assets/CityX/Character/NetworkedShoota.prefab"),
                Kind("wraith", "Assets/CityX/Character/NetworkedWraith.prefab"),
                Kind("giant", "Assets/CityX/Character/NetworkedGiant.prefab"),
            };
            foreach (NetworkManager manager in new[] { _server, _clients[0], _clients[1] })
            {
                var go = new GameObject($"EnemyNetwork {manager.name}");
                var network = go.AddComponent<EnemyNetwork>();
                network.Manager = manager;
                network.Kinds = kinds;
                _enemyNetworks.Add(network);
            }
            yield return null;
            yield return null;
        }

        private static EnemyNetwork.Kind Kind(string name, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
            Assert.IsNotNull(prefab, $"no {path} -- rebuild the massing scene");
            return new EnemyNetwork.Kind { Name = name, Prefab = prefab };
        }

        /// <summary>A asks for <paramref name="kind"/> beside its body; returns the enemy as A holds it and as B holds it.</summary>
        private IEnumerator SpawnFromA(string kind, Vector3 offset, System.Action<NetworkObject, NetworkObject> found)
        {
            EnemyNetwork aNetwork = _enemyNetworks[1];
            Assert.IsTrue(aNetwork.TrySpawn(kind, _aOnA.transform.position + offset, Quaternion.identity), "A's machine would not ask for an enemy");
            NetworkObject onA = null, onB = null;
            yield return WaitUntil(() =>
            {
                onA = EnemyOn(_clients[0]);
                onB = EnemyOn(_clients[1]);
                return onA != null && onB != null;
            }, $"the {kind} to appear on both players' machines");
            Assert.AreEqual(onA.ObjectId, onB.ObjectId);
            DisableCopyColliders();
            found(onA, onB);
        }

        private static NetworkObject EnemyOn(NetworkManager client)
        {
            foreach (NetworkObject nob in client.ClientManager.Objects.Spawned.Values)
            {
                if (nob != null && nob.GetComponent<NetworkedEnemy>() != null)
                {
                    return nob;
                }
            }
            return null;
        }

        /// <summary>The shared test scene holds the server's instance and each copy too: only the owner's real body stays solid.</summary>
        private void DisableCopyColliders()
        {
            foreach (NetworkedEnemy enemy in Object.FindObjectsByType<NetworkedEnemy>(FindObjectsSortMode.None))
            {
                if (enemy.IsOwner && enemy.NetworkManager == _clients[0])
                {
                    continue;
                }
                foreach (Collider c in enemy.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }
            }
        }

        private void TearDownEnemies()
        {
            foreach (NetworkedEnemy enemy in Object.FindObjectsByType<NetworkedEnemy>(FindObjectsSortMode.None))
            {
                if (enemy != null)
                {
                    Object.DestroyImmediate(enemy.gameObject);
                }
            }
            foreach (EnemyNetwork network in _enemyNetworks)
            {
                if (network != null)
                {
                    Object.DestroyImmediate(network.gameObject);
                }
            }
            _enemyNetworks.Clear();
            foreach (GiantSlam slam in Object.FindObjectsByType<GiantSlam>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(slam.gameObject);
            }
        }
#endif

        [UnityTest]
        public IEnumerator AnEnemyOnePlayerSpawnsIsRunByThemAndShownToTheOther()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("swordsman", new Vector3(0.0f, 0.0f, 6.0f), (a, b) => { onA = a; onB = b; });

            Assert.IsTrue(onA.IsOwner, "the enemy is not run by the player who asked for it");
            Assert.IsTrue(onA.GetComponent<SwordsmanBrain>().enabled, "the owner's swordsman has no brain running");
            Assert.IsFalse(onB.GetComponent<SwordsmanBrain>().enabled, "the other player's copy runs a brain of its own");
            Assert.IsNotNull(onB.GetComponentInChildren<SwordWeapon>(), "the copy holds no sword");

            // Moved by its owner: the copy follows.
            onA.transform.position += new Vector3(3.0f, 0.0f, 0.0f);
            yield return WaitUntil(() => Vector3.Distance(onA.transform.position, onB.transform.position) < 0.05f, "the copy to follow the owner's enemy");
#else
            Assert.Ignore("needs the editor to load the networked prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ABlowOnAnEnemysCopyLandsOnTheOwnersEnemyAndTheKillFellsTheCopyAndPaysTheKiller()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("swordsman", new Vector3(0.0f, 0.0f, 6.0f), (a, b) => { onA = a; onB = b; });
            var real = onA.GetComponent<PlayerVitals>();
            var copy = onB.GetComponent<PlayerVitals>();
            var ragdoll = onB.GetComponent<Ragdoll>();
            float before = real.Health;
            var killer = _bOnB.GetComponent<PlayerVitals>();
            int xpBefore = killer.Experience, levelBefore = killer.Level;
            onA.GetComponent<SwordsmanBrain>().enabled = false;   // held still: a guard or a parry of his own would make the blows uncertain

            DamageContext.Strike(copy, 10.0f, _bOnB.transform.position, 0.2f, DamageKind.Physical, 0.0f, onB.transform.position + Vector3.up, _bOnB.gameObject);
            yield return WaitUntil(() => real.Health < before - 1.0f, "B's blow on the copy to land on A's real swordsman");
            yield return WaitUntil(() => copy.Health < before - 1.0f, "the copy to show the owner's health");

            for (int i = 0; i < 30 && real.IsAlive; i++)
            {
                DamageContext.Strike(copy, 40.0f, _bOnB.transform.position, 0.2f, DamageKind.Physical, 0.0f, onB.transform.position + Vector3.up, _bOnB.gameObject);
                yield return new WaitForSeconds(0.1f);
            }
            Assert.IsFalse(real.IsAlive, "B's blows never killed A's swordsman");
            // The copy falls by itself, then lies as the owner's body does once its poses come (RagdollMirror): whichever word comes first.
            var mirror = onB.GetComponent<RagdollMirror>();
            yield return WaitUntil(() => copy.IsDead && ragdoll != null && (ragdoll.IsLimp || (mirror != null && mirror.IsMirroring)),
                "B's copy to fall when the owner's swordsman died");
            yield return WaitUntil(() => _enemyNetworks[2].CreditedXp > 0, "B to be paid the kill");
            Assert.AreEqual(real.XpRewardOnDeath, _enemyNetworks[2].CreditedXp, "B was not paid the swordsman's XP");
            Assert.IsTrue(killer.Level > levelBefore || killer.Experience == xpBefore + real.XpRewardOnDeath, "B's player did not get the XP");

            // Done with, the corpse is taken out of the session everywhere.
            EnemySession.Remove(onA.gameObject);
            yield return WaitUntil(() => EnemyOn(_clients[1]) == null, "the corpse to go from B's machine");
#else
            Assert.Ignore("needs the editor to load the networked prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AWraithIsHurtThroughItsCopyAndFadesOnEveryMachine()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("wraith", new Vector3(0.0f, 2.0f, 6.0f), (a, b) => { onA = a; onB = b; });
            var real = onA.GetComponent<Wraith>();
            var copy = onB.GetComponent<Wraith>();
            Assert.IsFalse(real.IsCopy);
            Assert.IsTrue(copy.IsCopy, "the other player's Wraith drifts by itself");

            ((IDamageable)copy).ApplyHit(5.0f, _bOnB.transform.position, 0.0f);
            yield return WaitUntil(() => real.Health < Wraith.MaxHealth - 1.0f, "B's blow on the copy to land on A's Wraith");
            yield return WaitUntil(() => copy.Health < Wraith.MaxHealth - 1.0f, "the copy to show its health");
            Assert.Greater(copy.Hurt, 0.0f, "the copy did not flinch");

            ((IDamageable)copy).ApplyHit(20.0f, _bOnB.transform.position, 0.0f);
            yield return WaitUntil(() => !real.IsAlive && !copy.IsAlive, "the Wraith to die on both machines");
            yield return WaitUntil(() => _enemyNetworks[2].CreditedXp == Wraith.XpForKill, "B to be paid the Wraith");
            yield return WaitUntil(() => EnemyOn(_clients[1]) == null, "the faded Wraith to be taken out of the session");
#else
            Assert.Ignore("needs the editor to load the networked prefabs.");
            yield break;
#endif
        }

        /// <summary>The giant (docs/GIANT.md): the other player's copy is as big as the owner's, and the slam the owner's giant lands heaves the ground on their screen too.</summary>
        [UnityTest]
        public IEnumerator AGiantsCopyIsAGiantAndItsSlamHeavesTheGroundOnTheOtherScreen()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("giant", new Vector3(0.0f, 0.0f, 20.0f), (a, b) => { onA = a; onB = b; });
            Assert.AreEqual(GiantRules.Scale, onB.transform.lossyScale.y, 1e-3f, "the other player's copy is not a giant");
            Assert.IsTrue(onA.GetComponent<GiantBrain>().enabled, "the owner's giant has no mind running");
            Assert.IsFalse(onB.GetComponent<GiantBrain>().enabled, "the copy runs a giant's mind of its own");

            var brain = onA.GetComponent<GiantBrain>();
            yield return WaitUntil(() => brain.Slams > 0, "the owner's giant to leap and land");
            // One slam is the owner's machine's own (with its blows); the other is B's copy showing it.
            yield return WaitUntil(() => Object.FindObjectsByType<GiantSlam>(FindObjectsSortMode.None).Length >= 2, "the copy to show the slam");
#else
            Assert.Ignore("needs the editor to load the networked prefabs.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AnotherPlayersCopyIsAPlayerAnEnemyCanGoFor()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            Assert.IsTrue(PlayerBodies.IsPlayer(_bOnA.gameObject), "on A's machine, B's copy is not counted as a player an enemy can fight");
            Assert.IsTrue(PlayerBodies.IsPlayer(_aOnA.gameObject), "A's own body is not counted as a player");
            yield break;
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
