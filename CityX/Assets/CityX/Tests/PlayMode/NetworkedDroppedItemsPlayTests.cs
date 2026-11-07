using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Dropped items over real sockets (a server and players in this process, each machine with its own record of what lies around): a weapon
    /// one player drops lies on the others' machines too; whoever picks it up gets it and it is gone everywhere; when two ask at once only one
    /// gets it; the server refuses a drop far from the dropper; and a player who joins later is shown what still lies around. The multiplayer-first
    /// rule (owner, 2026-10-17, dropping weapons on death).
    /// </summary>
    public sealed class NetworkedDroppedItemsPlayTests
    {
        private const string NetworkedPlayerPrefabPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private static ushort _nextPort = 46100;
        private const float TimeoutSeconds = 30.0f;

        private GameObject _ground;
        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<NetworkManager> _managers = new List<NetworkManager>();
        private readonly List<DroppedItems> _records = new List<DroppedItems>();

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            foreach (NetworkManager m in _managers)
            {
                if (m == null) continue;
                if (m.ClientManager.Started) m.ClientManager.StopConnection();
                if (m.ServerManager.Started) m.ServerManager.StopConnection(true);
            }
            yield return null;
            foreach (DroppedItems record in _records)
            {
                record.Clear();
            }
            _records.Clear();
            foreach (NetworkedPlayerIdentity identity in Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity != null) Object.DestroyImmediate(identity.gameObject);
            }
            foreach (GameObject go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            _managers.Clear();
            if (_ground != null) Object.Destroy(_ground);
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.Relay = null;
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator WaitUntil(System.Func<bool> done, string what)
        {
            float start = Time.realtimeSinceStartup;
            while (!done())
            {
                Assert.Less(Time.realtimeSinceStartup - start, TimeoutSeconds, $"timed out waiting for {what}");
                yield return null;
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
            }
        }

        /// <summary>A network manager, this machine's record of what lies around, and its DroppedItemsNetwork bound to both.</summary>
        private (NetworkManager manager, DroppedItems record, DroppedItemsNetwork network) Machine(string name)
        {
            GameObject prefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(prefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject go = Object.Instantiate(prefab);
            go.name = name;
            _made.Add(go);
            var manager = go.GetComponent<NetworkManager>();
            _managers.Add(manager);
            var record = new DroppedItems();
            _records.Add(record);
            var networkObject = new GameObject("DroppedItemsNetwork " + name);
            _made.Add(networkObject);
            var network = networkObject.AddComponent<DroppedItemsNetwork>();
            network.Manager = manager;
            network.Target = record;
            return (manager, record, network);
        }

        private Inventory Pockets(string name)
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go.AddComponent<Inventory>();
        }

        private static IEnumerator Join(NetworkManager client, ushort port)
        {
            Transport t = client.TransportManager.Transport;
            t.SetPort(port);
            t.SetClientAddress("127.0.0.1");
            client.ClientManager.StartConnection("127.0.0.1", port);
            yield return WaitUntil(() => client.IsClientStarted && client.ClientManager.Connection.IsAuthenticated, client.name + " to join");
        }

        private static NetworkConnection AtServer(NetworkManager server, NetworkManager client)
        {
            foreach (NetworkConnection connection in server.ServerManager.Clients.Values)
            {
                if (connection.ClientId == client.ClientManager.Connection.ClientId) return connection;
            }
            return null;
        }

        private static ItemDrop Sword(Vector3 at) => new ItemDrop
        {
            ItemId = ItemRegistry.SpikeSword, Position = at, Rotation = Quaternion.identity, Velocity = new Vector3(0.6f, 1.5f, 0.0f), Spin = Vector3.forward * 4.0f,
        };

        /// <summary>
        /// A sword let go 30 m up (a player dies on a roof or a footbridge, or falling) lands in the street: it can be picked up by whoever stands by it where it landed, and whoever is
        /// refused from too far off may ask again once nearer.
        /// </summary>
        [UnityTest]
        public IEnumerator AnItemThatFellALongWayIsTakenWhereItLanded([Values(true, false)] bool standingByIt)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPrefabPath);
            Assert.IsNotNull(playerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");
            ushort port = _nextPort++;
            (NetworkManager server, DroppedItems _, DroppedItemsNetwork _) = Machine("Fall Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            (NetworkManager a, DroppedItems onA, DroppedItemsNetwork netA) = Machine("Fall Client A");
            (NetworkManager b, DroppedItems onB, DroppedItemsNetwork netB) = Machine("Fall Client B");
            yield return Join(a, port);
            yield return Join(b, port);

            yield return WaitUntil(() => netA.StatesHeard > 0 && netB.StatesHeard > 0, "both players to be told what lies around");
            Inventory bPockets = Pockets("B's inventory");

            // (A's body starts to fall as soon as it appears: the sword goes at once, as it would from a body that dies up there.)
            const float Height = 30.0f;
            NetworkObject aBody = server.GetPooledInstantiated(playerPrefab, new Vector3(0.0f, Height, 0.0f), Quaternion.identity, asServer: true);
            server.ServerManager.Spawn(aBody, AtServer(server, a));
            NetworkObject bBody = server.GetPooledInstantiated(playerPrefab, new Vector3(standingByIt ? 3.0f : 60.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
            server.ServerManager.Spawn(bBody, AtServer(server, b));

            DroppedItem item = onA.Drop(Sword(new Vector3(0.4f, Height, 0.3f)), shared: true);
            Assert.IsNotNull(item, "the sword did not fall on the dropper's machine");
            long id = item.Id;
            yield return WaitUntil(() => onB.Has(id), "player B to see the sword");
            yield return WaitUntil(() => onB.Find(id) != null && onB.Find(id).transform.position.y < 3.0f, "the sword to land on player B's machine");

            Assert.IsTrue(onB.Take(id, bPockets), "player B could not ask for it");
            if (standingByIt)
            {
                yield return WaitUntil(() => !onB.Has(id), "the sword to be given to the player standing by it");
                Assert.IsTrue(bPockets.HasItem(ItemRegistry.SpikeSword), "the sword did not go into player B's inventory");
            }
            else
            {
                yield return WaitUntil(() => !onB.IsAskedFor(id), "player B to be told no, so they can ask again from nearer");
                Assert.IsTrue(onB.Has(id), "refused, but the sword is gone");
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// Bug hunt M11: a pack killed at once drops more than the server shares in a second, and each one it would not share lay on the dropper's
        /// screen as a ghost nobody could pick up. A burst well past what a spin or a smash makes is shared; past the spam guard, the dropper is
        /// told, and keeps the rest as their own to pick up. An ask for something the server no longer has is told it is gone.
        /// </summary>
        [UnityTest]
        public IEnumerator ABurstOfDropsIsSharedAndNoneIsLeftAsAGhost()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPrefabPath);
            Assert.IsNotNull(playerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");
            ushort port = _nextPort++;
            (NetworkManager server, DroppedItems _, DroppedItemsNetwork _) = Machine("Burst Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");
            (NetworkManager a, DroppedItems onA, DroppedItemsNetwork netA) = Machine("Burst Client A");
            (NetworkManager b, DroppedItems onB, DroppedItemsNetwork netB) = Machine("Burst Client B");
            yield return Join(a, port);
            yield return Join(b, port);
            yield return WaitUntil(() => netA.StatesHeard > 0 && netB.StatesHeard > 0, "both players to be told what lies around");
            NetworkObject aBody = server.GetPooledInstantiated(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
            server.ServerManager.Spawn(aBody, AtServer(server, a));
            yield return WaitSeconds(0.3f);

            const int Extra = 3;
            var ids = new List<long>();
            for (int i = 0; i < DroppedItemsNetwork.MaxDropsPerSecond + Extra; i++)
            {
                ids.Add(onA.Drop(Sword(new Vector3(i * 0.1f, 1.0f, 0.0f)), shared: true).Id);
            }
            yield return WaitUntil(() => netA.DropsRefused == Extra, "the dropper to be told of the drops past the spam guard");
            yield return WaitUntil(() => onB.Count == DroppedItemsNetwork.MaxDropsPerSecond, "the shared drops to reach player B");
            var kept = new List<long>();
            foreach (long id in ids)
            {
                if (onA.IsKeptHere(id)) kept.Add(id);
            }
            Assert.AreEqual(Extra, kept.Count, "the drops past the guard are not kept as the dropper's own");

            // Kept here, one is picked up at once, with no server to ask.
            Inventory aPockets = Pockets("A's inventory");
            Assert.IsTrue(onA.Take(kept[0], aPockets), "a drop the server would not share cannot be picked up");
            Assert.IsTrue(aPockets.HasItem(ItemRegistry.SpikeSword), "it did not go into the inventory");
            Assert.IsFalse(onA.Has(kept[0]));

            // An ask for an id the server does not have is answered: gone, not left waiting for good.
            netA.RequestTake(kept[1]);
            yield return WaitUntil(() => !onA.Has(kept[1]), "the server to say an item it does not have is gone");
#else
            yield break;
#endif
        }

        [TestCase(0.0f, 30.0f, 0.0f, 0.5f, 0.0f, ExpectedResult = true)]
        [TestCase(0.0f, 30.0f, 0.0f, 0.5f, 25.0f, ExpectedResult = true)]
        [TestCase(0.0f, 30.0f, 0.0f, 0.5f, 200.0f, ExpectedResult = false)]
        [TestCase(0.0f, 1.0f, 0.0f, 40.0f, 0.0f, ExpectedResult = false)]
        public bool AnItemFallsItDoesNotFly(float fromX, float fromY, float fromZ, float toY, float toZ)
        {
            return DroppedItemsNetwork.CouldHaveFallenTo(new Vector3(fromX, fromY, fromZ), new Vector3(fromX, toY, toZ));
        }

        [UnityTest]
        public IEnumerator ADroppedWeaponLiesOnEveryMachineAndOnlyThePlayerWhoPicksItUpFirstGetsIt()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPrefabPath);
            Assert.IsNotNull(playerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");

            ushort port = _nextPort++;
            (NetworkManager server, DroppedItems _, DroppedItemsNetwork _) = Machine("Drops Server");
            Transport st = server.TransportManager.Transport;
            st.SetPort(port);
            st.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => server.IsServerStarted, "the server to start");

            (NetworkManager a, DroppedItems onA, DroppedItemsNetwork netA) = Machine("Drops Client A");
            (NetworkManager b, DroppedItems onB, DroppedItemsNetwork netB) = Machine("Drops Client B");
            yield return Join(a, port);
            yield return Join(b, port);

            // Each player gets a body (the server checks a dropper's body is near the drop, and a taker's near the item): A's at the origin, B's 4 m along.
            NetworkObject aBody = server.GetPooledInstantiated(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
            server.ServerManager.Spawn(aBody, AtServer(server, a));
            NetworkObject bBody = server.GetPooledInstantiated(playerPrefab, new Vector3(4.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
            server.ServerManager.Spawn(bBody, AtServer(server, b));
            yield return WaitUntil(() => a.ClientManager.Objects.Spawned.Count >= 2 && b.ClientManager.Objects.Spawned.Count >= 2,
                "both players to appear on both clients");
            yield return WaitUntil(() => netA.StatesHeard > 0 && netB.StatesHeard > 0, "both players to be told what lies around on joining");
            Inventory aPockets = Pockets("A's inventory");
            Inventory bPockets = Pockets("B's inventory");

            // Player A dies holding a sword: it falls on A's machine at once, and lies on B's.
            DroppedItem onAItem = onA.Drop(Sword(new Vector3(0.4f, 1.6f, 0.3f)), shared: true);
            Assert.IsNotNull(onAItem, "the sword is not shown on the dropper's own machine");
            long id = onAItem.Id;
            Assert.Greater(id, 0L, "a shared drop has no id from the session");
            yield return WaitUntil(() => onB.Has(id), "player B to see the sword player A dropped");
            Assert.IsNotNull(onB.Find(id), "the sword is not shown on player B's machine");
            Assert.AreEqual(ItemRegistry.SpikeSword, onB.Find(id).ItemId);

            // Player B picks it up: B gets it, and it is gone from both machines.
            Assert.IsTrue(onB.Take(id, bPockets));
            yield return WaitUntil(() => bPockets.HasItem(ItemRegistry.SpikeSword), "player B to get the sword");
            yield return WaitUntil(() => !onA.Has(id), "the sword to be gone from player A's machine");
            Assert.IsFalse(onB.Has(id), "the sword still lies on player B's machine");
            Assert.IsFalse(onA.Take(id, aPockets), "player A could still pick up a sword player B took");

            // Two players ask for the same sword at once: one gets it.
            DroppedItem second = onA.Drop(Sword(new Vector3(1.0f, 1.6f, 0.0f)), shared: true);
            long secondId = second.Id;
            yield return WaitUntil(() => onB.Has(secondId), "player B to see the second sword");
            int bBefore = Count(bPockets, ItemRegistry.SpikeSword);
            Assert.IsTrue(onA.Take(secondId, aPockets));
            Assert.IsTrue(onB.Take(secondId, bPockets));
            yield return WaitUntil(() => !onA.Has(secondId) && !onB.Has(secondId), "the second sword to be gone from both machines");
            yield return WaitSeconds(0.3f);
            int got = Count(aPockets, ItemRegistry.SpikeSword) + Count(bPockets, ItemRegistry.SpikeSword) - bBefore;
            Assert.AreEqual(1, got, "two players asking at once must not both get the sword");

            // A drop 300 m from the dropper's body is refused.
            int heard = netB.DropsHeard;
            onA.Drop(Sword(new Vector3(300.0f, 1.6f, 0.0f)), shared: true);
            DroppedItem near = onA.Drop(Sword(new Vector3(-0.5f, 1.6f, 0.0f)), shared: true);
            long nearId = near.Id;
            yield return WaitUntil(() => onB.Has(nearId), "player B to see the third sword");
            Assert.AreEqual(heard + 1, netB.DropsHeard, "a sword dropped 300 m from player A's body reached player B");

            // A player who joins now is shown the sword still lying there.
            (NetworkManager c, DroppedItems onC, DroppedItemsNetwork netC) = Machine("Drops Client C");
            yield return Join(c, port);
            yield return WaitUntil(() => netC.StatesHeard > 0, "the late joiner to be told what lies around");
            Assert.AreEqual(1, netC.LastStateSize, "the late joiner was not told exactly the one sword still lying there");
            Assert.IsTrue(onC.Has(nearId), "the late joiner does not see the sword lying there");
#else
            yield break;
#endif
        }

        private static int Count(Inventory inventory, string itemId)
        {
            int n = 0;
            foreach (Inventory.StorageSlot slot in inventory.Storage)
            {
                if (!slot.IsEmpty && slot.ItemId == itemId) n += slot.Count;
            }
            return n;
        }
    }
}
