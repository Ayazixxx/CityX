using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using CityX.World;
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
                Kind("wraith_summon", "Assets/CityX/Character/NetworkedWraithSummon.prefab"),
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

        /// <summary>
        /// A fallen enemy's gear is taken once, by whoever asks first (docs/LOOTING.md, bodies): B takes the swordsman's helmet off B's copy of
        /// his body, it goes into B's inventory, and it comes off A's body too, where A can no longer take it.
        /// </summary>
        [UnityTest]
        public IEnumerator AFallenEnemysHelmetIsTakenOnceAndComesOffOnEveryScreen()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("swordsman", new Vector3(0.0f, 0.0f, 4.0f), (a, b) => { onA = a; onB = b; });
            onA.GetComponent<SwordsmanBrain>().enabled = false;
            onA.GetComponent<PlayerVitals>().TakeDamage(10000.0f);
            var lootOnA = onA.GetComponent<CorpseLoot>();
            var lootOnB = onB.GetComponent<CorpseLoot>();
            yield return WaitUntil(() => lootOnA.Container != null && lootOnB.Container != null, "the swordsman's body to be searchable on both machines");
            Assert.IsTrue(lootOnB.Offers(Items.ItemRegistry.KnightHelmet), "B's copy of his body does not list his helmet");

            var bInventory = _bOnB.GetComponent<Inventory>();
            var aInventory = _aOnA.GetComponent<Inventory>();
            Assert.IsFalse(lootOnB.Give(Items.ItemRegistry.KnightHelmet, bInventory), "in a session the take should be asked of the server, not given at once");
            yield return WaitUntil(() => bInventory.HasItem(Items.ItemRegistry.KnightHelmet)
                || bInventory.EquippedItemId(Items.EquipSlots.Helmet) == Items.ItemRegistry.KnightHelmet, "the helmet to go into B's inventory");
            yield return WaitUntil(() => !lootOnA.Offers(Items.ItemRegistry.KnightHelmet), "the helmet to come off A's view of the body");
            yield return null;
            Assert.AreEqual(0, onA.GetComponentsInChildren<CityX.Wearables.KnightHelmet>().Length, "A still sees the helmet on the body");
            Assert.AreEqual(0, onB.GetComponentsInChildren<CityX.Wearables.KnightHelmet>().Length, "B still sees the helmet on the body");
            Assert.IsFalse(lootOnA.Give(Items.ItemRegistry.KnightHelmet, aInventory), "A could take the helmet B already has");
            Assert.IsFalse(aInventory.HasItem(Items.ItemRegistry.KnightHelmet), "the helmet went to both players");
#else
            Assert.Ignore("needs the editor to load the networked prefabs.");
            yield break;
#endif
        }

        /// <summary>
        /// An enemy's head taken off with its helmet on (docs/ARMOUR.md): the helmet falls off as its item from the owner's body, and comes off
        /// B's copy of the body too, which drops nothing of its own.
        /// </summary>
        [UnityTest]
        public IEnumerator AnEnemysHelmetComesOffEveryCopyWithItsHead()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("swordsman", new Vector3(0.0f, 0.0f, 4.0f), (a, b) => { onA = a; onB = b; });
            onA.GetComponent<SwordsmanBrain>().enabled = false;
            yield return null;
            Animator animator = onA.GetComponentInChildren<Animator>();
            Vector3 neck = animator.GetBoneTransform(HumanBodyBones.Neck).position;
            Vector3 head = Vector3.LerpUnclamped(neck, animator.GetBoneTransform(HumanBodyBones.Head).position, 1.4f);   // in the head, past where it meets the neck
            var vitals = onA.GetComponent<PlayerVitals>();
            vitals.EndBlock();   // his brain ran a few frames before it was stopped: a guard it raised would catch the blow
            using (DamageContext.Cutting(true))
            {
                DamageContext.Strike(vitals, 10000.0f, head + Vector3.back * 1.5f, 0.0f, DamageKind.Physical, 1.0f, head);
            }
            Assert.IsTrue(vitals.IsDead, "setup: the blow did not kill him");
            var ownerCut = onA.GetComponent<Dismemberer>();
            var copyCut = onB.GetComponent<Dismemberer>();
            Assert.AreEqual(CityX.Character.BodyPart.Head, ownerCut.LastPart, "the blow did not take the swordsman's head");
            Assert.AreEqual(1, ownerCut.PiecesPoppedOff, "his helmet did not come off as its item");
            yield return WaitUntil(() => copyCut.LastPart == CityX.Character.BodyPart.Head, "B's copy to lose its head");
            yield return null;
            Assert.AreEqual(1, copyCut.PiecesPoppedOff, "B's copy kept the helmet");
            Assert.AreEqual(0, onA.GetComponentsInChildren<CityX.Wearables.KnightHelmet>().Length, "the helmet is still on A's body");
            Assert.AreEqual(0, onB.GetComponentsInChildren<CityX.Wearables.KnightHelmet>().Length, "the helmet is still on B's copy");
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

        /// <summary>
        /// The Wraith Staff's summon (docs/WRAITH_STAFF.md): asked for by A, it comes in run by A's machine on A's side, and the other player's
        /// copy knows it is a summon too, so B's blow on it does not land and B's machine's enemies fight it.
        /// </summary>
        [UnityTest]
        public IEnumerator ASummonIsOnItsSummonersSideOnEveryMachine()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("wraith_summon", new Vector3(0.0f, WraithDrift.HoverHeight, 3.0f), (a, b) => { onA = a; onB = b; });
            var real = onA.GetComponent<Wraith>();
            var copy = onB.GetComponent<Wraith>();
            Assert.IsTrue(real.IsSummoned, "A's summon is not on A's side");
            Assert.AreEqual(_aOnA.GetComponent<PlayerVitals>(), real.Master, "A's summon does not follow A");
            Assert.IsTrue(copy.IsSummoned, "B's machine takes A's summon for a wild Wraith");
            Assert.IsTrue(Opponents.IsSummon(onB.gameObject), "B's machine's enemies would not fight A's summon");
            Assert.AreEqual(HitResponse.Ignored,
                DamageContext.Strike(copy, 20.0f, _bOnB.transform.position, 0.0f, DamageKind.Physical, 0.0f, null, _bOnB.gameObject),
                "B's blow landed on A's summon");
            yield return null;
            Assert.AreEqual(Wraith.SummonedMaxHealth, real.Health, "B's blow hurt A's summon");
#else
            Assert.Ignore("needs the editor to load the networked prefabs.");
            yield break;
#endif
        }

        /// <summary>Owner, 2026-11-05: the bite. The machine that runs the Wraith hunts and bites, the other player's copy plays it at the same neck, with its blood.</summary>
        [UnityTest]
        public IEnumerator AWraithsBiteShowsOnTheOtherPlayersCopyWithItsBlood()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            Vector3 behind = _aOnA.transform.position - _aOnA.transform.forward * 4.0f + Vector3.up * WraithDrift.HoverHeight;
            NetworkObject onA = null, onB = null;
            yield return SpawnFromA("wraith", behind, (a, b) => { onA = a; onB = b; });
            var real = onA.GetComponent<Wraith>();
            var copy = onB.GetComponent<Wraith>();

            yield return WaitUntil(() => real.Phase == WraithPhase.Biting, "the Wraith to reach a neck");
            yield return WaitUntil(() => copy.Phase == WraithPhase.Biting, "the other player's copy to show the bite");
            Assert.IsNotNull(copy.Prey, "the copy was not told whose neck");
            Assert.AreEqual(real.Prey.GetComponent<NetworkObject>().ObjectId, copy.Prey.GetComponent<NetworkObject>().ObjectId, "the copy bites someone else");
            yield return WaitUntil(() => copy.BloodShown > 0, "the blood to show on the other player's machine");
            Assert.Greater(real.BloodShown, 0);
            yield return WaitUntil(() => copy.Phase == WraithPhase.Thrown, "the copy to let go when its owner does");
#else
            Assert.Ignore("needs the editor to load the networked prefabs.");
            yield break;
#endif
        }

        /// <summary>
        /// The world's own enemies (docs/MOB_SPAWNING.md): each machine asks for enemies at random spots round its own player, as far off
        /// as they appear. Players standing together share ONE budget (out of doors and indoors each their own), whoever's machine runs
        /// the enemies; a killed one stops counting, and only the machine that runs an enemy can call it killed.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWorldsEnemiesNearAPlaceAreOneBudgetForAllThePlayersThere()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            yield return StartEnemyNetworks();
            EnemyNetwork server = _enemyNetworks[0], a = _enemyNetworks[1], b = _enemyNetworks[2];
            var toA = new Dictionary<long, GameObject>();
            var toB = new Dictionary<long, GameObject>();
            a.WorldMobAnswered += (ticket, body) => toA[ticket] = body;
            b.WorldMobAnswered += (ticket, body) => toB[ticket] = body;
            // Further off than a /spawn may be put down: the world's enemies appear out of reach.
            Vector3 Where(int n) => _aOnA.transform.position + new Vector3(n * 3.0f, 2.0f, EnemyNetwork.SpawnReachMetres + 20.0f);
            Assert.Less(Vector3.Distance(_aOnA.transform.position, _bOnB.transform.position), 20.0f, "the two players are meant to stand together");

            // A fills the outdoor budget.
            for (int n = 1; n <= MobHabitats.MostOutdoors; n++)
            {
                Assert.IsTrue(a.TrySpawnWorldMob("wraith", Where(n), Quaternion.identity, n, false), "A's machine would not ask");
            }
            yield return WaitUntil(() => toA.Count == MobHabitats.MostOutdoors, "A to be answered");
            foreach (GameObject body in toA.Values)
            {
                Assert.IsNotNull(body, "A was refused an enemy with room under the budget");
                Assert.IsTrue(body.GetComponent<NetworkObject>().IsOwner, "A does not run the enemy it asked for");
            }
            Assert.AreEqual(MobHabitats.MostOutdoors, server.Spawned);
            yield return WaitUntil(() => EnemyOn(_clients[1]) != null, "B to see A's enemies");
            DisableCopyColliders();

            // B, standing with A, is refused another outdoor one (the budget is the place's, not each player's), and A too.
            Assert.IsTrue(b.TrySpawnWorldMob("wraith", Where(10), Quaternion.identity, 1L, false));
            Assert.IsTrue(a.TrySpawnWorldMob("wraith", Where(11), Quaternion.identity, 50L, false));
            yield return WaitUntil(() => toB.ContainsKey(1L) && toA.ContainsKey(50L), "B and A to be answered");
            Assert.IsNull(toB[1L], "B was given an enemy over the budget of the place it shares with A");
            Assert.IsNull(toA[50L], "A was given an enemy over the budget");
            Assert.AreEqual(MobHabitats.MostOutdoors, server.Spawned);

            // Indoor ones have a budget of their own.
            Assert.IsTrue(b.TrySpawnWorldMob("swordsman", Where(12), Quaternion.identity, 2L, true));
            yield return WaitUntil(() => toB.ContainsKey(2L), "B to be answered for an indoor one");
            Assert.IsNotNull(toB[2L], "the outdoor budget kept an indoor enemy out");
            Assert.IsTrue(toB[2L].GetComponent<NetworkObject>().IsOwner);
            DisableCopyColliders();

            // B cannot call A's enemy killed (the ticket is A's own): still refused.
            b.NoteWorldMobKilled(3L);
            Assert.IsTrue(b.TrySpawnWorldMob("wraith", Where(13), Quaternion.identity, 3L, false));
            yield return WaitUntil(() => toB.ContainsKey(3L), "B to be answered again");
            Assert.IsNull(toB[3L], "a player called another player's enemy killed");

            // A's first is killed: it lies there, but counts no longer, and B is given one.
            a.NoteWorldMobKilled(1L);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(b.TrySpawnWorldMob("wraith", Where(14), Quaternion.identity, 4L, false));
            yield return WaitUntil(() => toB.ContainsKey(4L), "B to be answered once one of A's is dead");
            Assert.IsNotNull(toB[4L], "a dead enemy still counted toward the budget");

            // One taken away (its player left it) counts no longer either.
            EnemySession.Remove(toA[2L]);
            yield return WaitUntil(() => toA[2L] == null, "A's enemy to be taken out of the session");
            Assert.IsTrue(a.TrySpawnWorldMob("wraith", Where(15), Quaternion.identity, 51L, false));
            yield return WaitUntil(() => toA.ContainsKey(51L), "A to be answered once one was taken away");
            Assert.IsNotNull(toA[51L], "an enemy taken out of the session still counted toward the budget");

            // A kind the session does not share is not asked for at all (the monster exists on one machine alone, today).
            Assert.IsTrue(a.InSession);
            Assert.IsFalse(a.Shares("crimsonclaw"));
            Assert.IsFalse(a.TrySpawnWorldMob("crimsonclaw", Where(16), Quaternion.identity, 52L, false));
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
