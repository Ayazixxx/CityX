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
            ItemId = ItemRegistry.Sword, Position = at, Rotation = Quaternion.identity, Velocity = new Vector3(0.6f, 1.5f, 0.0f), Spin = Vector3.forward * 4.0f,
        };

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
            Assert.AreEqual(ItemRegistry.Sword, onB.Find(id).ItemId);

            // Player B picks it up: B gets it, and it is gone from both machines.
            Assert.IsTrue(onB.Take(id, bPockets));
            yield return WaitUntil(() => bPockets.HasItem(ItemRegistry.Sword), "player B to get the sword");
            yield return WaitUntil(() => !onA.Has(id), "the sword to be gone from player A's machine");
            Assert.IsFalse(onB.Has(id), "the sword still lies on player B's machine");
            Assert.IsFalse(onA.Take(id, aPockets), "player A could still pick up a sword player B took");

            // Two players ask for the same sword at once: one gets it.
            DroppedItem second = onA.Drop(Sword(new Vector3(1.0f, 1.6f, 0.0f)), shared: true);
            long secondId = second.Id;
            yield return WaitUntil(() => onB.Has(secondId), "player B to see the second sword");
            int bBefore = Count(bPockets, ItemRegistry.Sword);
            Assert.IsTrue(onA.Take(secondId, aPockets));
            Assert.IsTrue(onB.Take(secondId, bPockets));
            yield return WaitUntil(() => !onA.Has(secondId) && !onB.Has(secondId), "the second sword to be gone from both machines");
            yield return WaitSeconds(0.3f);
            int got = Count(aPockets, ItemRegistry.Sword) + Count(bPockets, ItemRegistry.Sword) - bBefore;
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
