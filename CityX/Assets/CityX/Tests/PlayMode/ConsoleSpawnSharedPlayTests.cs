using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// An item put down with /spawn in a session is shared (owner, 2026-11-26: "the items i spawned in console werent viewable to the guy i was
    /// playing with"): it goes out as a dropped item, to the server and so to every player; alone, it is put down here as before. The drop's
    /// trip over the wire is NetworkedDroppedItemsPlayTests'.
    /// </summary>
    public sealed class ConsoleSpawnSharedPlayTests
    {
        private sealed class RecordingRelay : IDroppedItemRelay
        {
            public readonly List<ItemDrop> Sent = new List<ItemDrop>();
            private long _next = 1L << 32;

            public long NewId() => ++_next;

            public void SendDrop(ItemDrop drop) => Sent.Add(drop);

            public void RequestTake(long id)
            {
            }
        }

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.Relay = null;
        }

        private DevConsoleView Console()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            _made.Add(ground);
            var body = new GameObject("Body");
            _made.Add(body);
            var inventory = body.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry
                {
                    ItemId = ItemRegistry.Chain,
                    Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Chain.prefab"),
                },
            };
            PlayerVitals vitals = body.AddComponent<PlayerVitals>();
            vitals.Revive();
            var consoleObject = new GameObject("DevConsole");
            _made.Add(consoleObject);
            var view = consoleObject.AddComponent<DevConsoleView>();
            view.Vitals = vitals;
            view.Bind();
            return view;
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator InASessionASpawnedItemGoesToEveryone()
        {
            DevConsoleView console = Console();
            var relay = new RecordingRelay();
            DroppedItems.Shared.Relay = relay;
            console.Submit("/spawn chain");
            yield return null;
            Assert.AreEqual(1, relay.Sent.Count, "the spawned chain was not told to the other players");
            Assert.AreEqual(ItemRegistry.Chain, relay.Sent[0].ItemId);
            Assert.AreEqual(1, DroppedItems.Shared.Count, "the spawned chain does not lie here as a shared drop");
            Assert.IsNotNull(DroppedItems.Shared.Find(relay.Sent[0].Id), "the spawned chain is not shown here");
            Assert.AreEqual(0, CountLoose<CityX.Weapons.ChainWeapon>(), "a second, unshared chain was put down as well");
        }

        [UnityTest]
        public IEnumerator AloneASpawnedItemIsPutDownHereAsBefore()
        {
            DevConsoleView console = Console();
            console.Submit("/spawn chain");
            yield return null;
            Assert.AreEqual(0, DroppedItems.Shared.Count, "alone, the spawned chain became a dropped item");
            Assert.AreEqual(1, CountLoose<CityX.Weapons.ChainWeapon>(), "alone, /spawn chain put down no chain");
            foreach (var chain in Object.FindObjectsByType<CityX.Weapons.ChainWeapon>(FindObjectsSortMode.None))
            {
                _made.Add(chain.gameObject);
            }
        }

        /// <summary>The items of this kind lying about that are not a dropped item's model.</summary>
        private static int CountLoose<T>() where T : Component
        {
            int count = 0;
            foreach (T item in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (item.GetComponent<DroppedItem>() == null) count++;
            }
            return count;
        }
    }
}
