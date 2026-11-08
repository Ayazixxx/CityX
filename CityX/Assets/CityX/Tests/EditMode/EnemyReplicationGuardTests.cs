using System.Linq;
using CityX.Character;
using CityX.Weapons;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using FishNet.Object;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityX.Tests
{
    /// <summary>
    /// THE GUARD THAT KEEPS EVERY ENEMY SHARED (owner, 2026-10-27: "do the enemy sync between players"): each enemy /spawn offers to fight has a
    /// networked variant the city's EnemyNetwork spawns through the server, listed in FishNet's prefab collection (or no machine could spawn
    /// it), moved by its owner and with its brain run by the owner alone. A new enemy is not finished until it is here.
    /// </summary>
    public sealed class EnemyReplicationGuardTests
    {
        private const string CityScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const string PrefabCollectionPath = "Assets/DefaultPrefabObjects.asset";

        /// <summary>The fighting enemies /spawn puts down, and what runs each one's mind (null: it drives itself, as the Wraith does).</summary>
        private static readonly (string kind, System.Type brain)[] Enemies =
        {
            ("swordsman", typeof(SwordsmanBrain)),
            ("shoota", typeof(ShootaBrain)),
            ("wraith", null),
            ("wraith_summon", null),   // the Wraith Staff's summon (docs/WRAITH_STAFF.md)
        };

        [Test]
        public void EveryEnemyIsSpawnedForEveryoneThroughTheCitysEnemyNetwork()
        {
            Scene scene = EditorSceneManager.OpenScene(CityScenePath, OpenSceneMode.Additive);
            try
            {
                EnemyNetwork network = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnemyNetwork>(true)).FirstOrDefault();
                Assert.IsNotNull(network, "the city has no EnemyNetwork: /spawn would put enemies down for one player only -- rebuild the scene");
                var collection = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(PrefabCollectionPath);
                Assert.IsNotNull(collection, $"no FishNet prefab collection at {PrefabCollectionPath}");

                foreach ((string kind, System.Type brain) in Enemies)
                {
                    NetworkObject prefab = network.Kinds.FirstOrDefault(k => k.Name == kind).Prefab;
                    Assert.IsNotNull(prefab, $"the city's EnemyNetwork cannot spawn a {kind}");
                    bool listed = false;
                    for (int i = 0; i < collection.GetObjectCount(); i++)
                    {
                        listed |= collection.GetObject(true, i) == prefab;
                    }
                    Assert.IsTrue(listed, $"the networked {kind} is not in FishNet's prefab collection: no machine could spawn it");
                    Assert.IsNotNull(prefab.GetComponent<NetworkTransform>(), $"the networked {kind} is not moved on other players' machines");
                    var enemy = prefab.GetComponent<NetworkedEnemy>();
                    Assert.IsNotNull(enemy, $"the networked {kind} has no NetworkedEnemy: its brain would run on every machine");
                    if (brain != null)
                    {
                        Assert.IsTrue(enemy.OwnerOnly.Any(b => b != null && b.GetType() == brain),
                            $"the {kind}'s {brain.Name} is not switched off on other players' copies");
                        Assert.IsNotNull(prefab.GetComponent<NetworkedDamage>(), $"a blow on another player's copy of the {kind} would go nowhere");
                        Assert.IsNotNull(prefab.GetComponent<NetworkedAnimatorSync>(), $"other players would see the {kind} stand still");
                    }
                    else
                    {
                        Assert.IsNotNull(prefab.GetComponent<NetworkedWraith>(), $"a blow on another player's copy of the {kind} would go nowhere");
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ARateLimitCountsEachSenderAlonePerSecond()
        {
            var rate = new SenderRate(2);
            Assert.IsTrue(rate.Allow(1, 10.0f));
            Assert.IsTrue(rate.Allow(1, 10.2f));
            Assert.IsFalse(rate.Allow(1, 10.4f), "a third in the same second got through");
            Assert.IsTrue(rate.Allow(2, 10.4f), "another sender was held to the first one's count");
            Assert.IsTrue(rate.Allow(1, 11.1f), "a new second did not start the count again");
        }

        [Test]
        public void WireChecksRefuseBrokenNumbers()
        {
            Assert.IsFalse(WireChecks.Finite(new Vector3(float.NaN, 0.0f, 0.0f)));
            Assert.IsFalse(WireChecks.Finite(new Vector3(0.0f, float.PositiveInfinity, 0.0f)));
            Assert.IsTrue(WireChecks.Finite(new Vector3(1.0f, 2.0f, 3.0f)));
            Assert.IsTrue(WireChecks.IsRotation(Quaternion.Euler(10.0f, 20.0f, 30.0f)));
            Assert.IsFalse(WireChecks.IsRotation(new Quaternion(0.0f, 0.0f, 0.0f, 0.0f)), "a zero quaternion is no rotation");
        }
    }
}
